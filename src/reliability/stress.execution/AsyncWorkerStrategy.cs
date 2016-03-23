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
    public class AsyncWorkerStrategy : IWorkerStrategy
    {
        public void SpawnWorker(ITestPattern pattern, CancellationToken cancelToken)
        {
            Task throwaway = this.ExecuteWorkerAsync(pattern, cancelToken);
        }

        public async Task ExecuteWorkerAsync(ITestPattern pattern, CancellationToken cancelToken)
        {
            while (!cancelToken.IsCancellationRequested)
            {
                UnitTest test = pattern.GetNextTest();

                await this.ExecuteTestAsync(test, cancelToken);
            }
        }

        public async Task ExecuteTestAsync(UnitTest test, CancellationToken cancelToken)
        {
            TaskCompletionSource<object> executionCancelled = new TaskCompletionSource<object>();

            Task testTask = Task.Run(() => test.Execute());

            cancelToken.Register(() => executionCancelled.SetResult(null));

            //if the execution is cancelled before the test is complete that test will continue running
            //however this should only occur when the execution has run to duration so we return to allow the process to exit
            await Task.WhenAny(testTask, executionCancelled.Task);
        }
    }
}
