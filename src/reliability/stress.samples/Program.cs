// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// 

using stress.execution;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace stress.samples
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            CancellationTokenSource tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(20));

            new ConcurrentDictionaryLoadTesting().SimpleLoad(tokenSource.Token);
        }
    }
}
