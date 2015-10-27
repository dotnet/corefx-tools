using System;
using System.Threading;

namespace stress.execution
{
    public class TestExecutionLog
    {
        private long _execTime = 0;
        private int _failedCount = 0;
        private int _passedCount = 0;
        private int _currentCount = 0;

        public long ExecTime { get { return _execTime; } }

        public int FailedCount { get { return this._failedCount; } }
        public int PassedCount { get { return this._passedCount; } }

        public long BeginTest()
        {
            Interlocked.Increment(ref this._currentCount);

            return DateTime.Now.Ticks;
        }

        public void EndTest(long begin, bool pass)
        {
            Interlocked.Add(ref this._execTime, DateTime.Now.Ticks - begin);

            Interlocked.Decrement(ref this._currentCount);

            if (pass)
            {
                Interlocked.Increment(ref this._passedCount);
            }
            else
            {
                Interlocked.Increment(ref this._failedCount);
            }

        }
    }
}