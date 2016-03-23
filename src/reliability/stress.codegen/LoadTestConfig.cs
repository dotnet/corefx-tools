// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace stress.codegen
{
    public class LoadTestConfig
    {
        // The number of load tests we're generating for this template
        public int TestCount;

        // The number of unit tests in the load test
        public int NumTests;

        // The duration for this load test
        public TimeSpan Duration;

        // The number of workers used in the load test
        public int NumWorkers;

        // The unit test execution pattern for this load test
        public string TestPattern;

        //the load patern for this load test
        public string LoadPattern;

        // The execution strategy for this load test
        public string WorkerStrategy;

        public Dictionary<string, string> EnvironmentVariables;

        public LoadTestConfig()
        {
            EnvironmentVariables = new Dictionary<string, string>();
        }
    }
}
