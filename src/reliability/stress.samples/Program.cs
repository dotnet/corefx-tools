using stress.execution;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace stress.samples
{
    class Program
    {
        static void Main(string[] args)
        {
            CancellationTokenSource tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(20));

            new ConcurrentDictionaryLoadTesting().SimpleLoad(tokenSource.Token);
        }
    }

}
