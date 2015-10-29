// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace stress.codegen
{
    public interface ITestDiscoverer
    {
        UnitTestInfo[] GetTests(TestAssemblyInfo assembly);
    }

    public interface ISourceFileGenerator
    {
        void GenerateSourceFile(LoadTestInfo loadTest);
    }
}
