using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Filters;
using Nethermind.Blockchain.Receipts;
using Nethermind.Blockchain.TransactionPools;
using Nethermind.Blockchain.TransactionPools.Storages;
using Nethermind.Blockchain.Validators;
using Nethermind.Clique;
using Nethermind.Config;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Json;
using Nethermind.Core.Logging;
using Nethermind.Core.Model;
using Nethermind.Core.Specs;
using Nethermind.Core.Specs.ChainSpec;
using Nethermind.Db;
using Nethermind.Db.Config;
using Nethermind.Dirichlet.Numerics;
using Nethermind.Evm;
using Nethermind.Evm.Tracing;
using Nethermind.Facade;
using Nethermind.JsonRpc.Modules;
using Nethermind.JsonRpc.Modules.Admin;
using Nethermind.JsonRpc.Modules.DebugModule;
using Nethermind.JsonRpc.Modules.Eth;
using Nethermind.JsonRpc.Modules.Net;
using Nethermind.JsonRpc.Modules.Personal;
using Nethermind.JsonRpc.Modules.Trace;
using Nethermind.JsonRpc.Modules.TxPool;
using Nethermind.KeyStore;
using Nethermind.KeyStore.Config;
using Nethermind.Mining;
using Nethermind.Mining.Difficulty;
using Nethermind.Network;
using Nethermind.Network.Config;
using Nethermind.Network.Crypto;
using Nethermind.Network.Discovery;
using Nethermind.Network.Discovery.Lifecycle;
using Nethermind.Network.Discovery.Messages;
using Nethermind.Network.Discovery.RoutingTable;
using Nethermind.Network.Discovery.Serializers;
using Nethermind.Network.P2P;
using Nethermind.Network.P2P.Subprotocols.Eth;
using Nethermind.Network.P2P.Subprotocols.Eth.V63;
using Nethermind.Network.Rlpx;
using Nethermind.Network.Rlpx.Handshake;
using Nethermind.Runner.Config;
using Nethermind.Stats;
using Nethermind.Store;
using Nethermind.Wallet;
using Block = Nethermind.Core.Block;
using PingMessageSerializer = Nethermind.Network.P2P.PingMessageSerializer;
using PongMessageSerializer = Nethermind.Network.P2P.PongMessageSerializer;

namespace Nethermind.Runner.Runners
{
    public class EthStatsRunner : IRunner
    {
        private static ILogManager _logManager;
        private static ILogger _logger;

        private IRpcModuleProvider _rpcModuleProvider;
        private IConfigProvider _configProvider;
        private IInitConfig _initConfig;
        private INetworkHelper _networkHelper;

        public EthStatsRunner(IRpcModuleProvider rpcModuleProvider, IConfigProvider configurationProvider, ILogManager logManager)
        {
            _logManager = logManager ?? throw new ArgumentNullException(nameof(logManager));
            _logger = _logManager.GetClassLogger();

            //InitRlp();
            _configProvider = configurationProvider ?? throw new ArgumentNullException(nameof(configurationProvider));
            _rpcModuleProvider = rpcModuleProvider ?? throw new ArgumentNullException(nameof(rpcModuleProvider));
            _initConfig = configurationProvider.GetConfig<IInitConfig>();
            //_perfService = new PerfService(_logManager) { LogOnDebug = _initConfig.LogPerfStatsOnDebug };
            _networkHelper = new NetworkHelper(_logger);
        }
            Task IRunner.Start()
        {
            throw new NotImplementedException();
        }

        Task IRunner.StopAsync()
        {
            throw new NotImplementedException();
        }
    }
}
