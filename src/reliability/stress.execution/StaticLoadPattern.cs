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
    public class StaticLoadPattern : ILoadPattern
    {
        public int WorkerCount { get; set; }

        public async Task ExecuteAsync(ITestPattern testPattern, IWorkerStrategy execStrategy, CancellationToken cancelToken)
        {
            SemaphoreSlim cancelSignaled = new SemaphoreSlim(0, 1);

            using (cancelToken.Register(() => cancelSignaled.Release()))
            {
                for (int i = 0; i < this.WorkerCount; i++)
                {
                    execStrategy.SpawnWorker(testPattern, cancelToken);
                }

                await cancelSignaled.WaitAsync();
            }
        }

        public void Execute(ITestPattern testPattern, IWorkerStrategy execStrategy, CancellationToken cancelToken)
        {
            ManualResetEventSlim cancelSignaled = new ManualResetEventSlim(false);

            using (cancelToken.Register(() => cancelSignaled.Set()))
            {
                for (int i = 0; i < this.WorkerCount; i++)
                {
                    execStrategy.SpawnWorker(testPattern, cancelToken);
                }

                cancelSignaled.Wait();
            }
        }
    }
}
