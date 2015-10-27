using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace stress.execution
{
    public interface ILoadPattern
    {
        Task ExecuteAsync(ITestPattern testPattern, IWorkerStrategy workerStrategy, CancellationToken cancelToken);

        void Execute(ITestPattern testPattern, IWorkerStrategy workerStrategy, CancellationToken cancelToken);
    }
}
