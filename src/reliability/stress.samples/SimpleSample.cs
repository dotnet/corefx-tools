// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// 

using stress.execution;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace stress.samples
{
    public class SimpleSample
    {
        [Load("00:00:20")]
        public void SimpleLoad(CancellationToken cancelToken)
        {
            var unitTests = new UnitTest[] { new UnitTest(this.Foo), new UnitTest(this.Bar), new UnitTest(this.Shz), new UnitTest(this.Nit) };

            var testPattern = new RandomTestPattern();

            testPattern.Initialize(0, unitTests);

            var workerStrategy = new DedicatedThreadWorkerStrategy();

            var loadPattern = new StaticLoadPattern() { WorkerCount = 24 };

            Task t = loadPattern.ExecuteAsync(testPattern, workerStrategy, cancelToken);

            Task.Delay(3000).GetAwaiter().GetResult();

            var rootLoadPattern = new StaticLoadPattern() { WorkerCount = 500 };

            //add a burst of execution
            rootLoadPattern.Execute(new UnitTest(Root), workerStrategy, new CancellationTokenSource(10000).Token);

            //wait for original workers to complete
            t.GetAwaiter().GetResult();
        }

        public void Foo()
        {
            Console.WriteLine("Hi I'm Foo :)");
        }

        public void Bar()
        {
            Console.WriteLine("Hi I'm Bar :|");
        }

        public void Shz()
        {
            Console.WriteLine("Hi I'm Shz :(");
        }

        public void Nit()
        {
            Console.WriteLine("Hi I'm Nit :$");
        }

        public void Root()
        {
            Console.WriteLine("Hi I'm Root %");
        }
    }

    public class ConcurrentDictionaryLoadTesting
    {
        [Load("00:00:20")]
        public void SimpleLoad(CancellationToken cancelToken)
        {
            var workerStrategy = new DedicatedThreadWorkerStrategy();

            var readPattern = new StaticLoadPattern() { WorkerCount = 5 };

            Task t = readPattern.ExecuteAsync(new UnitTest(ConcurrentRead), workerStrategy, cancelToken);

            var writePattern = new StaticLoadPattern() { WorkerCount = 1 };

            //add a burst of execution
            Task t1 = writePattern.ExecuteAsync(new UnitTest(ConcurrentWrite), workerStrategy, cancelToken);

            //wait for original workers to complete
            Task.WaitAll(t, t1);
        }

        public void OnlyReaders(CancellationToken cancelToken)
        {
            var workerStrategy = new DedicatedThreadWorkerStrategy();

            //setup dictionary

            var readPattern = new StaticLoadPattern() { WorkerCount = 5 };

            readPattern.ExecuteAsync(new UnitTest(ConcurrentRead), workerStrategy, cancelToken).Wait();
        }

        [Fact]
        public void ConcurrentRead()
        {
            int key = _rand.Next(100);
            int val;

            if (!_dictoinary.TryGetValue(key, out val))
            {
                val = -1;
            }

            Console.WriteLine($"GET: {key} = {val}");
        }

        [Fact]
        public void ConcurrentWrite()
        {
            int key = _rand.Next(100);
            int val = _rand.Next(100);

            _dictoinary[key] = val;
            Console.WriteLine($"GET: {key} = {val}");
        }

        private ConcurrentDictionary<int, int> _dictoinary = new ConcurrentDictionary<int, int>();
        private Random _rand = new Random();
    }
}
