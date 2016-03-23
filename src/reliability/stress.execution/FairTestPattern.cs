// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace stress.execution
{
    public class FairTestPattern : ITestPattern
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
            return _tests[GetNextTestIndex(_tests)];
        }

        private int GetNextTestIndex(IList<UnitTest> tests)
        {
            long total = 0;

            long[] buckets = new long[tests.Count];

            int i;

            for (i = 0; i < buckets.Length; i++)
            {
                total += tests[i].Log.ExecTime + 1;

                buckets[i] = total;
            }

            byte[] randBytes = new byte[16];

            _rand.NextBytes(randBytes);

            long lRand = BitConverter.ToInt64(randBytes, 0) % total;

            for (i = 0; i < buckets.Length; i++)
            {
                if (lRand < buckets[i])
                {
                    break;
                }
            }

            return i;
        }
    }
}
