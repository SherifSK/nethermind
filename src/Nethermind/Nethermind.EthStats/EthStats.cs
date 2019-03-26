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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Nethermind.Dirichlet.Numerics;


namespace Nethermind.EthStats
{
    class EthStats
    {
        public static string ETH_VERSION;
        public static string NET_VERSION;
	    public static string PROTOCOL_VERSION;
	    public static string API_VERSION;
	    public static string COINBASE;

        public static string INSTANCE_NAME;
        public static string WS_SECRET;

        public static bool PENDING_WORKS;
        public static int MAX_BLOCKS_HISTORY;
        public static int UPDATE_INTERVAL;
        public static int PING_INTERVAL;
        public static int MINERS_LIMIT;
        public static int MAX_HISTORY_UPDATE;
        public static int MAX_CONNECTION_ATTEMPTS;
        public static int CONNECTION_ATTEMPTS_TIMEOUT;

        private readonly string _configurationFilePath;
        private readonly string _sectionNameSuffix;

        public EthStats(string configurationFilePath, string sectionNameSuffix = "Settings")
        {
            _configurationFilePath = configurationFilePath;
            _sectionNameSuffix = sectionNameSuffix;

            //values according to eth-net-intelligence-api - to be discussed later
            PENDING_WORKS = true;
            MAX_BLOCKS_HISTORY = 40;
            UPDATE_INTERVAL = 5000;
            PING_INTERVAL = 3000;
            MINERS_LIMIT = 5;
            MAX_HISTORY_UPDATE = 50;
            MAX_CONNECTION_ATTEMPTS = 50;
            CONNECTION_ATTEMPTS_TIMEOUT = 1000;
            this.Info = new EthStatsInfo();
            this.Stats = new EthStatsStats();
        }

        public void LoadConfig(Type type)
        {
            var configurationBuilder = new ConfigurationBuilder()
                .AddJsonFile("process.json")
                .AddEnvironmentVariables();

            IConfiguration config = configurationBuilder.Build();

            //Doesn't work...null all the time
            //var test = Configuration.Get("Data:TargetFolderLocations");
        }


        public EthStatsInfo Info { get; }
        public EthStatsStats Stats { get; }
        public string Id { get; }

        public UInt256 LastBlock { get; }
	    public string LastStats { get; }
	    public UInt256 LastFetch { get; }
	    public UInt256 LastPending { get; }
        public UInt256 Tries { get; }
	    public UInt256 Down { get; }
	    public UInt256 LastSent { get; }
	    public UInt256 Latency { get; }

	    public bool Web3 { get; }
	    public bool Socket { get; }

	    public string LatestQueue { get; }
	    public bool PendingFilter { get; }
	    public bool ChainFilter { get; }
	    public bool UpdateInterval { get; }
	    public bool PingInterval { get; }
	    public bool ConnectionInterval { get; }

	    public UInt256 LastBlockSentAt { get; }
	    public UInt256 LastChainLog { get; }
	    public UInt256 LastPendingLog { get; }
	    public UInt256 ChainDebouncer { get; }
	    public UInt256 Chan_Min_Time { get; }
	    public UInt256 Max_Chain_Debouncer { get; }
	    public UInt256 Chain_Debouncer_Cnt { get; }
	    public UInt256 Connection_Attempts { get; }
	    public UInt256 TimeOffset { get; }
    }
}