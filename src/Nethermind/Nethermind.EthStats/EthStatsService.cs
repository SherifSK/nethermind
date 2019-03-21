using System;

using Nethermind.JsonRpc.Client;
using Nethermind.Core;
using Nethermind.Core.Logging;

namespace Nethermind.EthStats
{
    class EthStatsService : BasicJsonRpcClient
    {
        public EthStatsService(Uri uri, IJsonSerializer jsonSerializer, ILogManager logManager) : base(
            uri, jsonSerializer, logManager)
        {

        }
        
    }
}
