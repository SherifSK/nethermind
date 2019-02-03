﻿/*
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
using System.Security;
using Nethermind.Config;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Logging;
using Nethermind.Facade;
using Nethermind.JsonRpc.Data;

namespace Nethermind.JsonRpc.Modules.Personal
{
    public class PersonalModule : ModuleBase, IPersonalModule
    {
        private readonly IPersonalBridge _bridge;

        public PersonalModule(IPersonalBridge bridge, IConfigProvider configProvider, ILogManager logManager, IJsonSerializer jsonSerializer) : base(configProvider, logManager, jsonSerializer)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        public override ModuleType ModuleType => ModuleType.Personal;

        public ResultWrapper<Address> personal_importRawKey(byte keyData, string passphrase)
        {
            throw new NotImplementedException();
        }

        public ResultWrapper<Address[]> personal_listAccounts()
        {
            return ResultWrapper<Address[]>.Success(_bridge.ListAccounts());
        }

        public ResultWrapper<bool> personal_lockAccount(Address address)
        {
            _bridge.LockAccount(address);

            return ResultWrapper<bool>.Success(true);
        }

        [RequiresSecurityReview("Consider removing any operations that allow to provide passphrase in JSON RPC")]
        public ResultWrapper<bool> personal_unlockAccount(Address address, string passphrase)
        {
            var notSecuredHere = new SecureString();
            foreach (char c in passphrase)
            {
                notSecuredHere.AppendChar(c);
            }
            
            notSecuredHere.MakeReadOnly();

            _bridge.UnlockAccount(address, notSecuredHere);

            return ResultWrapper<bool>.Success(true);
        }

        [RequiresSecurityReview("Consider removing any operations that allow to provide passphrase in JSON RPC")]
        public ResultWrapper<Address> personal_newAccount(string passphrase)
        {
            var notSecuredHere = new SecureString();
            foreach (char c in passphrase)
            {
                notSecuredHere.AppendChar(c);
            }
            
            notSecuredHere.MakeReadOnly();
            return ResultWrapper<Address>.Success(_bridge.NewAccount(notSecuredHere));
        }

        [RequiresSecurityReview("Consider removing any operations that allow to provide passphrase in JSON RPC")]
        public ResultWrapper<Keccak> personal_sendTransaction(TransactionForRpc transaction, string passphrase)
        {
            throw new NotImplementedException();
        }

        public ResultWrapper<Address> personal_ecRecover(byte[] message, byte[] signature)
        {
            throw new NotImplementedException();
        }

        [RequiresSecurityReview("Consider removing any operations that allow to provide passphrase in JSON RPC")]
        public ResultWrapper<byte[]> personal_sign(byte[] message, Address address, string passphrase = null)
        {
            throw new NotImplementedException();
        }
    }
}