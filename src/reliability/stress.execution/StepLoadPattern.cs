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
    public class StepLoadPattern : ILoadPattern
    {
        public TimeSpan StepDuration { get; set; }

        public int StepIncrease { get; set; }

        public int MaxWorkers { get; set; }

        public int WorkerCount { get; private set; }

        public void Execute(ITestPattern testPattern, IWorkerStrategy execStrategy, CancellationToken cancelToken)
        {
            this.ExecuteAsync(testPattern, execStrategy, cancelToken).GetAwaiter().GetResult();
        }

        public async Task ExecuteAsync(ITestPattern testPattern, IWorkerStrategy workerStrategy, CancellationToken cancelToken)
        {
            while ((!cancelToken.IsCancellationRequested) && (this.WorkerCount < this.MaxWorkers))
            {
                this.Step(testPattern, workerStrategy, cancelToken);

                if ((!cancelToken.IsCancellationRequested) && (this.WorkerCount < this.MaxWorkers))
                {
                    await Task.Delay(this.StepDuration, cancelToken);
                }
            }
        }

        public void Step(ITestPattern testPattern, IWorkerStrategy workerStrategy, CancellationToken cancelToken)
        {
            for (int i = 0; !cancelToken.IsCancellationRequested && i < this.StepIncrease && this.WorkerCount <= this.MaxWorkers; i++)
            {
                workerStrategy.SpawnWorker(testPattern, cancelToken);
            }
        }
    }
}
