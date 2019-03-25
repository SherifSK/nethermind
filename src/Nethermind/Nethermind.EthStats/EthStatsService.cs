using System;

using Nethermind.Core;
using Nethermind.Core.Logging;
using Nethermind.JsonRpc.Client;

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
