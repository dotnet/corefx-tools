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
            this._rand = new Random(seed);
            this._tests = tests;
        }

        public UnitTest GetNextTest()
        {
            int idx = this._rand.Next(this._tests.Count);

            return this._tests[idx];
        }
    }
}
