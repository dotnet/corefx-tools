// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
