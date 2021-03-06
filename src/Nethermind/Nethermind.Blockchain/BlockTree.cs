/*
 * Copyright (c) 2018 Demerzel Solutions Limited
 * This file is part of the Nethermind library.
 *
 * The Nethermind library is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * The Nethermind library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nethermind.Blockchain.TransactionPools;
using Nethermind.Blockchain.Validators;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Encoding;
using Nethermind.Core.Extensions;
using Nethermind.Core.Logging;
using Nethermind.Core.Specs;
using Nethermind.Dirichlet.Numerics;
using Nethermind.Store;

namespace Nethermind.Blockchain
{
    public class BlockTree : IBlockTree
    {
        private readonly LruCache<Keccak, Block> _blockCache = new LruCache<Keccak, Block>(64);

        private readonly LruCache<BigInteger, ChainLevelInfo> _blockInfoCache =
            new LruCache<BigInteger, ChainLevelInfo>(64);

        private const int MaxQueueSize = 10_000_000;

        public const int DbLoadBatchSize = 1000;

        private UInt256 _currentDbLoadBatchEnd;

        private ReaderWriterLockSlim _blockInfoLock = new ReaderWriterLockSlim();

        private readonly IDb _blockDb;

        private ConcurrentDictionary<UInt256, HashSet<Keccak>> _invalidBlocks = new ConcurrentDictionary<UInt256, HashSet<Keccak>>();
        private readonly BlockDecoder _blockDecoder = new BlockDecoder();
        private readonly IDb _blockInfoDb;
        private readonly ILogger _logger;
        private readonly ISpecProvider _specProvider;
        private readonly ITransactionPool _transactionPool;

        // TODO: validators should be here
        public BlockTree(
            IDb blockDb,
            IDb blockInfoDb,
            ISpecProvider specProvider,
            ITransactionPool transactionPool,
            ILogManager logManager)
        {
            _logger = logManager?.GetClassLogger() ?? throw new ArgumentNullException(nameof(logManager));
            _blockDb = blockDb;
            _blockInfoDb = blockInfoDb;
            _specProvider = specProvider;
            _transactionPool = transactionPool;

            ChainLevelInfo genesisLevel = LoadLevel(0, true);
            if (genesisLevel != null)
            {
                if (genesisLevel.BlockInfos.Length != 1)
                {
                    // just for corrupted test bases
                    genesisLevel.BlockInfos = new[] {genesisLevel.BlockInfos[0]};
                    PersistLevel(0, genesisLevel);
                    //throw new InvalidOperationException($"Genesis level in DB has {genesisLevel.BlockInfos.Length} blocks");
                }

                LoadBestKnown();

                if (genesisLevel.BlockInfos[0].WasProcessed)
                {
                    Block genesisBlock = Load(genesisLevel.BlockInfos[0].BlockHash).Block;
                    Genesis = genesisBlock.Header;
                    LoadHeadBlock();
                }
            }

            if (_logger.IsInfo) _logger.Info($"Block tree initialized, last processed is {Head?.ToString(BlockHeader.Format.Short) ?? "0"}, best queued is {BestSuggested?.Number.ToString() ?? "0"}, best known is {BestKnownNumber}");
        }

        private void LoadBestKnown()
        {
            BigInteger headNumber = Head == null ? -1 : (BigInteger) Head.Number;
            BigInteger left = headNumber;
            BigInteger right = headNumber + MaxQueueSize;

            while (left != right)
            {
                BigInteger index = left + (right - left) / 2;
                ChainLevelInfo level = LoadLevel(index, true);
                if (level == null)
                {
                    right = index;
                }
                else
                {
                    left = index + 1;
                }
            }


            BigInteger result = left - 1;
            if (result < 0)
            {
                throw new InvalidOperationException($"Bets known is {result}");
            }

            BestKnownNumber = (UInt256) (result);
        }

        public bool CanAcceptNewBlocks { get; private set; } = true; // no need to sync it at the moment

        public async Task LoadBlocksFromDb(
            CancellationToken cancellationToken,
            UInt256? startBlockNumber = null,
            int batchSize = DbLoadBatchSize,
            int maxBlocksToLoad = int.MaxValue)
        {
            try
            {
                CanAcceptNewBlocks = false;

                byte[] deletePointer = _blockInfoDb.Get(DeletePointerAddressInDb);
                if (deletePointer != null)
                {
                    Keccak deletePointerHash = new Keccak(deletePointer);
                    if (_logger.IsInfo) _logger.Info($"Cleaning invalid blocks starting from {deletePointer}");
                    CleanInvalidBlocks(deletePointerHash);
                }

                if (startBlockNumber == null)
                {
                    startBlockNumber = Head?.Number ?? 0;
                }
                else
                {
                    Head = startBlockNumber == 0 ? null : FindBlock(startBlockNumber.Value - 1)?.Header;
                }

                BigInteger blocksToLoad = BigInteger.Min(FindNumberOfBlocksToLoadFromDb(), maxBlocksToLoad);
                if (blocksToLoad == 0)
                {
                    if (_logger.IsInfo) _logger.Info("Found no blocks to load from DB.");
                }
                else
                {
                    if (_logger.IsInfo) _logger.Info($"Found {blocksToLoad} blocks to load from DB starting from current head block {Head?.ToString(BlockHeader.Format.Short)}.");
                }

                UInt256 blockNumber = startBlockNumber.Value;
                for (int i = 0; i < blocksToLoad; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    ChainLevelInfo level = LoadLevel(blockNumber);
                    if (level == null)
                    {
                        break;
                    }

                    BigInteger maxDifficultySoFar = 0;
                    BlockInfo maxDifficultyBlock = null;
                    for (int blockIndex = 0; blockIndex < level.BlockInfos.Length; blockIndex++)
                    {
                        if (level.BlockInfos[blockIndex].TotalDifficulty > maxDifficultySoFar)
                        {
                            maxDifficultyBlock = level.BlockInfos[blockIndex];
                            maxDifficultySoFar = maxDifficultyBlock.TotalDifficulty;
                        }
                    }

                    level = null;
                    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                    if (level != null)
                        // ReSharper disable once HeuristicUnreachableCode
                    {
                        // ReSharper disable once HeuristicUnreachableCode
                        throw new InvalidOperationException("just be aware that this level can be deleted by another thread after here");
                    }

                    if (maxDifficultyBlock == null)
                    {
                        throw new InvalidOperationException($"Expected at least one block at level {blockNumber}");
                    }

                    Block block = FindBlock(maxDifficultyBlock.BlockHash, false);
                    if (block == null)
                    {
                        if (_logger.IsError) _logger.Error($"Could not find block {maxDifficultyBlock.BlockHash}. DB load cancelled.");
                        _dbBatchProcessed?.SetResult(null);
                        break;
                    }

                    BestSuggested = block.Header;
                    NewBestSuggestedBlock?.Invoke(this, new BlockEventArgs(block));

                    if (i % batchSize == batchSize - 1 && !(i == blocksToLoad - 1) && (Head.Number + (UInt256) batchSize) < blockNumber)
                    {
                        if (_logger.IsInfo)
                        {
                            _logger.Info($"Loaded {i + 1} out of {blocksToLoad} blocks from DB into processing queue, waiting for processor before loading more.");
                        }

                        _dbBatchProcessed = new TaskCompletionSource<object>();
                        using (cancellationToken.Register(() => _dbBatchProcessed.SetCanceled()))
                        {
                            _currentDbLoadBatchEnd = blockNumber - (UInt256) batchSize;
                            await _dbBatchProcessed.Task;
                        }
                    }

                    blockNumber++;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.Info($"Canceled loading blocks from DB at block {blockNumber}");
                }

                if (_logger.IsInfo)
                {
                    _logger.Info($"Completed loading blocks from DB at block {blockNumber}");
                }
            }
            finally
            {
                CanAcceptNewBlocks = true;
            }
        }

        public event EventHandler<BlockEventArgs> BlockAddedToMain;

        public event EventHandler<BlockEventArgs> NewBestSuggestedBlock;

        public event EventHandler<BlockEventArgs> NewHeadBlock;

        public BlockHeader Genesis { get; private set; }
        public BlockHeader Head { get; private set; }
        public BlockHeader BestSuggested { get; private set; }
        public UInt256 BestKnownNumber { get; private set; }
        public int ChainId => _specProvider.ChainId;

        public AddBlockResult SuggestBlock(Block block)
        {
#if DEBUG
            /* this is just to make sure that we do not fall into this trap when creating tests */
            if (block.StateRoot == null && !block.IsGenesis)
            {
                throw new InvalidDataException($"State root is null in {block.ToString(Block.Format.Short)}");
            }
#endif

            if (!CanAcceptNewBlocks)
            {
                return AddBlockResult.CannotAccept;
            }

            if (_invalidBlocks.ContainsKey(block.Number) && _invalidBlocks[block.Number].Contains(block.Hash))
            {
                return AddBlockResult.InvalidBlock;
            }

            if (block.Number == 0)
            {
                if (BestSuggested != null)
                {
                    throw new InvalidOperationException("Genesis block should be added only once"); // TODO: make sure it cannot happen
                }
            }
            else if (IsKnownBlock(block.Number, block.Hash))
            {
                if (_logger.IsTrace)
                {
                    _logger.Trace($"Block {block.Hash} already known.");
                }

                return AddBlockResult.AlreadyKnown;
            }
            else if (!IsKnownBlock(block.Number - 1, block.Header.ParentHash))
            {
                if (_logger.IsTrace)
                {
                    _logger.Trace($"Could not find parent ({block.Header.ParentHash}) of block {block.Hash}");
                }

                return AddBlockResult.UnknownParent;
            }

            _blockDb.Set(block.Hash, Rlp.Encode(block).Bytes);
//            _blockCache.Set(block.Hash, block);

            // TODO: when reviewing the entire data chain need to look at the transactional storing of level and block
            SetTotalDifficulty(block);
            SetTotalTransactions(block);
            BlockInfo blockInfo = new BlockInfo(block.Hash, block.TotalDifficulty.Value, block.TotalTransactions.Value);

            try
            {
                _blockInfoLock.EnterWriteLock();
                UpdateOrCreateLevel(block.Number, blockInfo);
            }
            finally
            {
                _blockInfoLock.ExitWriteLock();
            }


            if (block.IsGenesis || block.TotalDifficulty > (BestSuggested?.TotalDifficulty ?? 0))
            {
                BestSuggested = block.Header;
                NewBestSuggestedBlock?.Invoke(this, new BlockEventArgs(block));
            }

            return AddBlockResult.Added;
        }

        public Block FindBlock(Keccak blockHash, bool mainChainOnly)
        {
            (Block block, BlockInfo _, ChainLevelInfo level) = Load(blockHash);
            if (block == null)
            {
                return null;
            }

            if (mainChainOnly)
            {
                // TODO: double hash comparison
                bool isMain = level.HasBlockOnMainChain && level.BlockInfos[0].BlockHash.Equals(blockHash);
                return isMain ? block : null;
            }

            return block;
        }

        // TODO: since finding by hash will be faster it will be worth to refactor this part
        public Block[] FindBlocks(Keccak blockHash, int numberOfBlocks, int skip, bool reverse)
        {
            if (blockHash == null) throw new ArgumentNullException(nameof(blockHash));

            Block[] result = new Block[numberOfBlocks];
            Block startBlock = FindBlock(blockHash, true);
            if (startBlock == null)
            {
                return result;
            }

            for (int i = 0; i < numberOfBlocks; i++)
            {
                int blockNumber = (int) startBlock.Number + (reverse ? -1 : 1) * (i + i * skip);
                Block ithBlock = FindBlock((UInt256) blockNumber);
                result[i] = ithBlock;
            }

            return result;
        }

        private Keccak GetBlockHashOnMain(UInt256 blockNumber)
        {
            if (blockNumber.Sign < 0)
            {
                throw new ArgumentException($"{nameof(blockNumber)} must be greater or equal zero and is {blockNumber}",
                    nameof(blockNumber));
            }

            ChainLevelInfo level = LoadLevel(blockNumber);
            if (level == null)
            {
                return null;
            }

            if (level.HasBlockOnMainChain)
            {
                return level.BlockInfos[0].BlockHash;
            }

            if (level.BlockInfos.Length > 0)
            {
                throw new InvalidOperationException($"Unexpected request by number for a block {blockNumber} that is not on the main chain");
            }

            return null;
        }

        public Block FindBlock(UInt256 blockNumber)
        {
            Keccak hash = GetBlockHashOnMain(blockNumber);
            return Load(hash).Block;
        }

        public void DeleteInvalidBlock(Block invalidBlock)
        {
            if(_logger.IsDebug) _logger.Debug($"Deleting invalid block {invalidBlock.ToString(Block.Format.FullHashAndNumber)}");
            
            _invalidBlocks.AddOrUpdate(
                invalidBlock.Number,
                number => new HashSet<Keccak> {invalidBlock.Hash},
                (number, set) =>
                {
                    set.Add(invalidBlock.Hash);
                    return set;
                });

            BestSuggested = Head;

            try
            {
                CanAcceptNewBlocks = false;
            }
            finally
            {
                CleanInvalidBlocks(invalidBlock.Hash);
                CanAcceptNewBlocks = true;
            }
        }

        private void CleanInvalidBlocks(Keccak deletePointer)
        {
            BlockHeader deleteHeader = FindHeader(deletePointer);
            UInt256 currentNumber = deleteHeader.Number;
            Keccak currentHash = deleteHeader.Hash;
            Keccak nextHash = null;
            ChainLevelInfo nextLevel = null;

            while (true)
            {
                ChainLevelInfo currentLevel = nextLevel ?? LoadLevel(currentNumber);
                nextLevel = LoadLevel(currentNumber + 1);

                bool shouldRemoveLevel = false;

                if (currentLevel != null) // preparing update of the level (removal of the invalid branch block)
                {
                    if (currentLevel.BlockInfos.Length == 1)
                    {
                        shouldRemoveLevel = true;
                    }
                    else
                    {
                        for (int i = 0; i < currentLevel.BlockInfos.Length; i++)
                        {
                            if (currentLevel.BlockInfos[0].BlockHash == currentHash)
                            {
                                currentLevel.BlockInfos = currentLevel.BlockInfos.Where(bi => bi.BlockHash != currentHash).ToArray();
                                break;
                            }
                        }
                    }
                }

                if (nextLevel != null) // just finding what the next descendant will be
                {
                    if (nextLevel.BlockInfos.Length == 1)
                    {
                        nextHash = nextLevel.BlockInfos[0].BlockHash;
                    }
                    else
                    {
                        for (int i = 0; i < nextLevel.BlockInfos.Length; i++)
                        {
                            BlockHeader potentialDescendant = FindHeader(nextLevel.BlockInfos[i].BlockHash);
                            if (potentialDescendant.ParentHash == currentHash)
                            {
                                nextHash = potentialDescendant.Hash;
                                break;
                            }
                        }
                    }

                    UpdateDeletePointer(nextHash);
                }
                else
                {
                    UpdateDeletePointer(null);
                }

                try
                {
                    _blockInfoLock.EnterWriteLock();
                    if (shouldRemoveLevel)
                    {
                        BestKnownNumber = UInt256.Min(BestKnownNumber, currentNumber - 1);
                        _blockInfoCache.Delete(currentNumber);
                        _blockInfoDb.Delete(currentNumber);
                    }
                    else
                    {
                        PersistLevel(currentNumber, currentLevel);
                    }
                }
                finally
                {
                    _blockInfoLock.ExitWriteLock();
                }

                if(_logger.IsInfo) _logger.Info($"Deleting invalid block {currentHash} at level {currentNumber}");
                _blockCache.Delete(currentHash);
                _blockDb.Delete(currentHash);

                if (nextHash == null)
                {
                    break;
                }

                currentNumber++;
                currentHash = nextHash;
                nextHash = null;
            }
        }

        public bool IsMainChain(Keccak blockHash)
        {
            BigInteger number = LoadNumberOnly(blockHash);
            ChainLevelInfo level = LoadLevel(number);
            return level.HasBlockOnMainChain && level.BlockInfos[0].BlockHash.Equals(blockHash);
        }

        public bool WasProcessed(UInt256 number, Keccak blockHash)
        {
            ChainLevelInfo levelInfo = LoadLevel(number);
            int? index = FindIndex(blockHash, levelInfo);
            if (index == null)
            {
                throw new InvalidOperationException($"Not able to find block {blockHash} index on the chain level");
            }

            return levelInfo.BlockInfos[index.Value].WasProcessed;
        }

        public void UpdateMainChain(Block[] processedBlocks)
        {
            if (processedBlocks.Length == 0)
            {
                return;
            }

            bool ascendingOrder = true;
            if (processedBlocks.Length > 1)
            {
                if (processedBlocks[processedBlocks.Length - 1].Number < processedBlocks[0].Number)
                {
                    ascendingOrder = false;
                }
            }

#if DEBUG
            for (int i = 0; i < processedBlocks.Length; i++)
            {
                if (i != 0)
                {
                    if (ascendingOrder && processedBlocks[i].Number != processedBlocks[i - 1].Number + 1)
                    {
                        throw new InvalidOperationException("Update main chain invoked with gaps");
                    }

                    if (!ascendingOrder && processedBlocks[i - 1].Number != processedBlocks[i].Number + 1)
                    {
                        throw new InvalidOperationException("Update main chain invoked with gaps");
                    }
                }
            }
#endif

            UInt256 lastNumber = ascendingOrder ? processedBlocks[processedBlocks.Length - 1].Number : processedBlocks[0].Number;
            UInt256 previousHeadNumber = Head?.Number ?? UInt256.Zero;
            try
            {
                _blockInfoLock.EnterWriteLock();
                if (previousHeadNumber > lastNumber)
                {
                    for (UInt256 i = 0; i < UInt256.Subtract(previousHeadNumber, lastNumber); i++)
                    {
                        UInt256 levelNumber = previousHeadNumber - i;

                        ChainLevelInfo level = LoadLevel(levelNumber);
                        level.HasBlockOnMainChain = false;
                        PersistLevel(levelNumber, level);
                    }
                }

                for (int i = 0; i < processedBlocks.Length; i++)
                {
                    _blockCache.Set(processedBlocks[i].Hash, processedBlocks[i]);
                    MoveToMain(processedBlocks[i]);
                }
            }
            finally
            {
                _blockInfoLock.ExitWriteLock();
            }
        }

        private TaskCompletionSource<object> _dbBatchProcessed;

        private void MoveToMain(Block block)
        {
            if (_logger.IsTrace) _logger.Trace($"Moving {block.ToString(Block.Format.Short)} to main");

            ChainLevelInfo level = LoadLevel(block.Number);
            int? index = FindIndex(block.Hash, level);
            if (index == null)
            {
                throw new InvalidOperationException($"Cannot move unknown block {block.ToString(Block.Format.FullHashAndNumber)} to main");
            }


            BlockInfo info = level.BlockInfos[index.Value];
            info.WasProcessed = true;
            if (index.Value != 0)
            {
                (level.BlockInfos[index.Value], level.BlockInfos[0]) = (level.BlockInfos[0], level.BlockInfos[index.Value]);
            }

            // tks: in testing chains we have a chain full of processed blocks that we process again
            //if (level.HasBlockOnMainChain)
            //{
            //    throw new InvalidOperationException("When moving to main encountered a block in main on the same level");
            //}

            level.HasBlockOnMainChain = true;
            PersistLevel(block.Number, level);

            BlockAddedToMain?.Invoke(this, new BlockEventArgs(block));

            if (block.IsGenesis || block.TotalDifficulty > (Head?.TotalDifficulty ?? 0))
            {
                if (block.Number == 0)
                {
                    Genesis = block.Header;
                }

                if (block.TotalDifficulty == null)
                {
                    throw new InvalidOperationException("Head block with null total difficulty");
                }

                UpdateHeadBlock(block);
            }

            for (int i = 0; i < block.Transactions.Length; i++)
            {
                _transactionPool.RemoveTransaction(block.Transactions[i].Hash);
            }

            if (_logger.IsTrace) _logger.Trace($"Block {block.ToString(Block.Format.Short)} added to main chain");
        }

        [Todo(Improve.Refactor, "Look at this magic -1 behaviour, never liked it, now when it is split between BestKnownNumber and Head it is even worse")]
        private BigInteger FindNumberOfBlocksToLoadFromDb()
        {
            BigInteger headNumber = Head == null ? -1 : (BigInteger) Head.Number;
            return BestKnownNumber - headNumber;
        }

        private void LoadHeadBlock()
        {
            byte[] data = _blockInfoDb.Get(HeadAddressInDb) ?? _blockDb.Get(HeadAddressInDb);
            if (data != null)
            {
                BlockHeader headBlockHeader;
                try
                {
                    headBlockHeader = Rlp.Decode<BlockHeader>(data.AsRlpContext(), RlpBehaviors.AllowExtraData);
                }
                catch (Exception)
                {
                    // in the old times we stored the whole block here, I guess it can be removed now
                    headBlockHeader = Rlp.Decode<Block>(data.AsRlpContext(), RlpBehaviors.AllowExtraData).Header;
                }

                ChainLevelInfo level = LoadLevel(headBlockHeader.Number);
                int? index = FindIndex(headBlockHeader.Hash, level);
                if (!index.HasValue)
                {
                    throw new InvalidDataException("Head block data missing from chain info");
                }

                headBlockHeader.TotalDifficulty = level.BlockInfos[index.Value].TotalDifficulty;
                headBlockHeader.TotalTransactions = level.BlockInfos[index.Value].TotalTransactions;

                Head = BestSuggested = headBlockHeader;
            }
        }

        public bool IsKnownBlock(UInt256 number, Keccak blockHash)
        {
            if (number > BestKnownNumber)
            {
                return false;
            }

            if (blockHash == Head?.Hash)
            {
                return true;
            }

            if (_blockCache.Get(blockHash) != null)
            {
                return true;
            }

            ChainLevelInfo level = LoadLevel(number);
            if (level == null)
            {
                return false;
            }

            return FindIndex(blockHash, level).HasValue;
        }

        internal static Keccak HeadAddressInDb = Keccak.Zero;
        internal static Keccak DeletePointerAddressInDb = new Keccak(new BitArray(32 * 8, true).ToBytes());

        private void UpdateDeletePointer(Keccak hash)
        {
            if (hash == null)
            {
                _blockInfoDb.Delete(DeletePointerAddressInDb);
                return;
            }

            if (_logger.IsInfo) _logger.Info($"Deleting an invalid block or its descendant {hash}");
            _blockInfoDb.Set(DeletePointerAddressInDb, hash.Bytes);
        }

        private void UpdateHeadBlock(Block block)
        {
            if (block.IsGenesis)
            {
                Genesis = block.Header;
            }

            Head = block.Header;
            _blockInfoDb.Set(HeadAddressInDb, Rlp.Encode(Head).Bytes);
            NewHeadBlock?.Invoke(this, new BlockEventArgs(block));
            if (_dbBatchProcessed != null)
            {
                if (block.Number == _currentDbLoadBatchEnd)
                {
                    TaskCompletionSource<object> completionSource = _dbBatchProcessed;
                    _dbBatchProcessed = null;
                    completionSource.SetResult(null);
                }
            }
        }

        private void UpdateOrCreateLevel(UInt256 number, BlockInfo blockInfo)
        {
            ChainLevelInfo level = LoadLevel(number, false);
            if (level != null)
            {
                BlockInfo[] blockInfos = new BlockInfo[level.BlockInfos.Length + 1];
                for (int i = 0; i < level.BlockInfos.Length; i++)
                {
                    blockInfos[i] = level.BlockInfos[i];
                }

                blockInfos[blockInfos.Length - 1] = blockInfo;
                level.BlockInfos = blockInfos;
            }
            else
            {
                if (number > BestKnownNumber)
                {
                    BestKnownNumber = number;
                }

                level = new ChainLevelInfo(false, new[] {blockInfo});
            }

            PersistLevel(number, level);
        }

        /* error-prone: all methods that load a level, change it and then persist need to execute everything under a lock */
        private void PersistLevel(BigInteger number, ChainLevelInfo level)
        {
            _blockInfoCache.Set(number, level);
            _blockInfoDb.Set(number, Rlp.Encode(level).Bytes);
        }

        private (BlockInfo Info, ChainLevelInfo Level) LoadInfo(BigInteger number, Keccak blockHash)
        {
            ChainLevelInfo chainLevelInfo = LoadLevel(number);
            if (chainLevelInfo == null)
            {
                return (null, null);
            }

            int? index = FindIndex(blockHash, chainLevelInfo);
            return index.HasValue ? (chainLevelInfo.BlockInfos[index.Value], chainLevelInfo) : (null, chainLevelInfo);
        }

        private int? FindIndex(Keccak blockHash, ChainLevelInfo level)
        {
            for (int i = 0; i < level.BlockInfos.Length; i++)
            {
                if (level.BlockInfos[i].BlockHash.Equals(blockHash))
                {
                    return i;
                }
            }

            return null;
        }

        private ChainLevelInfo LoadLevel(BigInteger number, bool forceLoad = true)
        {
            if (number > BestKnownNumber && !forceLoad)
            {
                return null;
            }

            ChainLevelInfo chainLevelInfo = _blockInfoCache.Get(number);
            if (chainLevelInfo == null)
            {
                byte[] levelBytes = _blockInfoDb.Get(number);
                if (levelBytes == null)
                {
                    return null;
                }

                chainLevelInfo = Rlp.Decode<ChainLevelInfo>(new Rlp(levelBytes));
            }

            return chainLevelInfo;
        }

        // TODO: use headers store or some simplified RLP decoder for number only or hash to number store
        private UInt256 LoadNumberOnly(Keccak blockHash)
        {
            Block block = _blockCache.Get(blockHash);
            if (block != null)
            {
                return block.Number;
            }

            byte[] blockData = _blockDb.Get(blockHash);
            if (blockData == null)
            {
                throw new InvalidOperationException(
                    $"Not able to retrieve block number for an unknown block {blockHash}");
            }

            block = _blockDecoder.Decode(blockData.AsRlpContext(), RlpBehaviors.AllowExtraData);
            _blockCache.Set(blockHash, block);
            return block.Number;
        }

        public BlockHeader FindHeader(Keccak blockHash)
        {
            Block block = _blockCache.Get(blockHash);
            if (block == null)
            {
                byte[] data = _blockDb.Get(blockHash);
                if (data == null)
                {
                    return null;
                }

                block = _blockDecoder.Decode(data.AsRlpContext(), RlpBehaviors.AllowExtraData);
                _blockCache.Set(blockHash, block);
            }

            BlockHeader header = block.Header;
            BlockInfo blockInfo = LoadInfo(header.Number, header.Hash).Info;
            header.TotalTransactions = blockInfo.TotalTransactions;
            if (_logger.IsTrace) _logger.Trace($"Updating total transactions of the main chain to {header.TotalTransactions}");
            header.TotalDifficulty = blockInfo.TotalDifficulty;
            if (_logger.IsTrace) _logger.Trace($"Updating total difficulty of the main chain to {header.TotalDifficulty}");

            return header;
        }

        public BlockHeader FindHeader(UInt256 number)
        {
            Keccak hash = GetBlockHashOnMain(number);
            if (hash == null)
            {
                return null;
            }

            return FindHeader(hash);
        }

        private (Block Block, BlockInfo Info, ChainLevelInfo Level) Load(Keccak blockHash)
        {
            if (blockHash == null || blockHash == Keccak.Zero)
            {
                return (null, null, null);
            }

            Block block = _blockCache.Get(blockHash);
            if (block == null)
            {
                byte[] data = _blockDb.Get(blockHash);
                if (data == null)
                {
                    return (null, null, null);
                }

                block = _blockDecoder.Decode(data.AsRlpContext(), RlpBehaviors.AllowExtraData);
                _blockCache.Set(blockHash, block);
            }

            (BlockInfo blockInfo, ChainLevelInfo level) = LoadInfo(block.Number, block.Hash);
            if (level == null || blockInfo == null)
            {
                // TODO: this is here because storing block data is not transactional
                // TODO: would be great to remove it, he?
                SetTotalDifficulty(block);
                SetTotalTransactions(block);
                blockInfo = new BlockInfo(block.Hash, block.TotalDifficulty.Value, block.TotalTransactions.Value);
                try
                {
                    _blockInfoLock.EnterWriteLock();
                    UpdateOrCreateLevel(block.Number, blockInfo);
                }
                finally
                {
                    _blockInfoLock.ExitWriteLock();
                }

                (blockInfo, level) = LoadInfo(block.Number, block.Hash);
            }
            else
            {
                block.Header.TotalDifficulty = blockInfo.TotalDifficulty;
                block.Header.TotalTransactions = blockInfo.TotalTransactions;
            }

            return (block, blockInfo, level);
        }

        private void SetTotalDifficulty(Block block)
        {
            if (_logger.IsTrace)
            {
                _logger.Trace($"Calculating total difficulty for {block}");
            }

            if (block.Number == 0)
            {
                block.Header.TotalDifficulty = block.Difficulty;
            }
            else
            {
                Block parent = this.FindParent(block.Header);
                if (parent == null)
                {
                    throw new InvalidOperationException($"An orphaned block on the chain {block}");
                }

                if (parent.TotalDifficulty == null)
                {
                    throw new InvalidOperationException(
                        $"Parent's {nameof(parent.TotalDifficulty)} unknown when calculating for {block}");
                }

                block.Header.TotalDifficulty = parent.TotalDifficulty + block.Difficulty;
            }

            if (_logger.IsTrace)
            {
                _logger.Trace($"Calculated total difficulty for {block} is {block.TotalDifficulty}");
            }
        }

        private void SetTotalTransactions(Block block)
        {
            if (block.Number == 0)
            {
                block.Header.TotalTransactions = (ulong) block.Transactions.Length;
            }
            else
            {
                Block parent = this.FindParent(block.Header);
                if (parent == null)
                {
                    throw new InvalidOperationException($"An orphaned block on the chain {block}");
                }

                if (parent.TotalTransactions == null)
                {
                    throw new InvalidOperationException(
                        $"Parent's {nameof(parent.TotalTransactions)} unknown when calculating for {block}");
                }

                block.Header.TotalTransactions = parent.TotalTransactions + (ulong) block.Transactions.Length;
            }
        }
    }
}