using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace stress.execution
{
    public interface IWorkerStrategy
    {
        void SpawnWorker(ITestPattern pattern, CancellationToken cancelToken);
    }
}
