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
using Newtonsoft.Json;

namespace Nethermind.EthStats
{
    class EthStats
    {
        private static IConfiguration _config;

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

            this.Info = new EthStatsInfo();
            this.Stats = new EthStatsStats();

            LoadConfig();
            Init();
        }

        public void LoadConfig(/*Type type*/)
        {
            var configurationBuilder = new ConfigurationBuilder()
                .AddJsonFile("process.json")
                .AddEnvironmentVariables();

            _config = configurationBuilder.Build();
        }

        public void Init()
        {
            IConfigurationSection sectionEnv = _config.GetSection("env");

            INSTANCE_NAME = sectionEnv.GetValue(typeof(string), "INSTANCE_NAME") as string;
            WS_SECRET = sectionEnv.GetValue(typeof(string), "WS_SECRET") as string;

            //values according to eth-net-intelligence-api - to be discussed later
            PENDING_WORKS = true;
            MAX_BLOCKS_HISTORY = 40;
            UPDATE_INTERVAL = 5000;
            PING_INTERVAL = 3000;
            MINERS_LIMIT = 5;
            MAX_HISTORY_UPDATE = 50;
            MAX_CONNECTION_ATTEMPTS = 50;
            CONNECTION_ATTEMPTS_TIMEOUT = 1000;

            string nodeEnv = sectionEnv.GetValue(typeof(string), "NODE_ENV") as string;
            if (INSTANCE_NAME == "" && nodeEnv.Equals("production"))
            {
                Console.WriteLine("No instance name specified!");
                Environment.Exit(0);
            }

            this.Info.Name = INSTANCE_NAME;
            this.Info.Contact = sectionEnv.GetValue(typeof(string), "CONTACT_DETAILS") as string;
            this.Info.Port = ((int)sectionEnv.GetValue(typeof(int), "CONTACT_DETAILS")) == 0 ? 
                ((int)sectionEnv.GetValue(typeof(int), "CONTACT_DETAILS")) : 30303;
            OperatingSystem os_info = System.Environment.OSVersion;
            this.Info.Os = os_info.Platform.ToString();
            this.Info.Os_v = os_info.Version.ToString();
            //client: pjson.version,

            this.LastStats = JsonConvert.SerializeObject(this.Stats);
        }


        public EthStatsInfo Info { get; set; }
        public EthStatsStats Stats { get; set; }
        public string Id { get; set; }

        public UInt256 LastBlock { get; set; }
	    public string LastStats { get; set; }
	    public UInt256 LastFetch { get; set; }
	    public UInt256 LastPending { get; set; }
        public UInt256 Tries { get; set; }
	    public UInt256 Down { get; set; }
	    public UInt256 LastSent { get; set; }
	    public UInt256 Latency { get; set; }

	    public bool Web3 { get; set; }
	    public bool Socket { get; set; }

	    public string LatestQueue { get; set; }
	    public bool PendingFilter { get; set; }
	    public bool ChainFilter { get; set; }
	    public bool UpdateInterval { get; set; }
	    public bool PingInterval { get; set; }
	    public bool ConnectionInterval { get; set; }

	    public UInt256 LastBlockSentAt { get; set; }
	    public UInt256 LastChainLog { get; set; }
	    public UInt256 LastPendingLog { get; set; }
	    public UInt256 ChainDebouncer { get; set; }
	    public UInt256 Chan_Min_Time { get; set; }
	    public UInt256 Max_Chain_Debouncer { get; set; }
	    public UInt256 Chain_Debouncer_Cnt { get; set; }
	    public UInt256 Connection_Attempts { get; set; }
	    public UInt256 TimeOffset { get; set; }
    }
}