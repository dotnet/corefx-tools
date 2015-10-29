// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace stress.execution
{
    public class RandomTestPattern : ITestPattern
    {
        private Random _rand;
        private IList<UnitTest> _tests;


        public void Initialize(int seed, IList<UnitTest> tests)
        {
            _rand = new Random(seed);
            _tests = tests;
        }

        public UnitTest GetNextTest()
        {
            int idx = _rand.Next(_tests.Count);

            return _tests[idx];
        }
    }
}
