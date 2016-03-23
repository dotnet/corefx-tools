// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace stress.execution
{
    public class DedicatedThreadWorkerStrategy : IWorkerStrategy
    {
        public void SpawnWorker(UnitTest test, CancellationToken cancelToken)
        {
            Task t = new Task(() => RunWorker(test, cancelToken), TaskCreationOptions.LongRunning);

            t.Start();
        }

        public void SpawnWorker(ITestPattern pattern, CancellationToken cancelToken)
        {
            Task t = new Task(() => RunWorker(pattern, cancelToken), TaskCreationOptions.LongRunning);

            t.Start();
        }

        private void RunWorker(ITestPattern pattern, CancellationToken cancelToken)
        {
            while (!cancelToken.IsCancellationRequested)
            {
                UnitTest t = pattern.GetNextTest();

                t.Execute();
            }
        }

        private void RunWorker(UnitTest test, CancellationToken cancelToken)
        {
            while (!cancelToken.IsCancellationRequested)
            {
                test.Execute();
            }
        }
    }
}
