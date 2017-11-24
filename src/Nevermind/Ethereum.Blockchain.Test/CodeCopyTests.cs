﻿using NUnit.Framework;

namespace Ethereum.Blockchain.Test
{
    [TestFixture]
    public class CodeCopyTests : BlockchainTestBase
    {
        [TestCaseSource(nameof(LoadTests), new object[] { "stCodeCopyTest" })]
        public void Test(BlockchainTest test)
        {
            RunTest(test);
        }
    }
}