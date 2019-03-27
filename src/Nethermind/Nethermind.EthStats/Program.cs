using System;
using System.Collections.Generic;
using System.Text;

namespace Nethermind.EthStats
{
    class Program
    {
        public static void Main(string[] args)
        {
            EthStats ethStats = new EthStats(Environment.CurrentDirectory);
        }

    }
}
