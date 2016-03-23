// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
