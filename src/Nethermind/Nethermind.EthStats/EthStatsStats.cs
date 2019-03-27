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
 using Nethermind.Dirichlet.Numerics;
 using Nethermind.Core;

namespace Nethermind.EthStats
{
    class EthStatsStats
    {

        public EthStatsStats()
        {
            this.Active = false;
            this.Mining = false;
            this.Syncing = false;
        }

        public bool Active { get; }
        public bool Mining { get; }
        public UInt256 HashRate { get; }
        public int Peers { get; }
        public UInt256 GasPrice { get; }
        public Block block {get;}
        public bool Syncing { get; }
        public UInt256 UpTime { get; }
    }
}