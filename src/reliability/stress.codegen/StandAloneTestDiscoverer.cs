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
    public class StandAloneTestDiscoverer : ITestDiscoverer
    {
        public UnitTestInfo[] GetTests(TestAssemblyInfo assemblyInfo)
        {
            List<string> assmRefs = new List<string>();

            foreach (var assmName in assemblyInfo.Assembly.GetReferencedAssemblies())
            {
                assmRefs.Add(assmName.Name);
            }

            UnitTestInfo[] tests = null;

            MethodInfo entryInfo = assemblyInfo.Assembly.EntryPoint;

            if (entryInfo != null)
            {
                UnitTestInfo test = new UnitTestInfo()
                {
                    AssemblyPath = assemblyInfo.Assembly.Location,
                    ReferenceInfo = assemblyInfo.ReferenceInfo,
                    Class = new TestClassInfo { FullName = entryInfo.DeclaringType.FullName, IsAbstract = entryInfo.DeclaringType.IsAbstract, IsGenericType = entryInfo.DeclaringType.IsGenericType, IsPublic = entryInfo.DeclaringType.IsPublic },
                    Method = new TestMethodInfo { Name = entryInfo.Name, IsAbstract = entryInfo.IsAbstract, IsGenericMethodDefinition = entryInfo.IsGenericMethodDefinition, IsPublic = entryInfo.IsPublic, IsStatic = entryInfo.IsStatic },
                };

                tests = new UnitTestInfo[] { test };
            }

            return tests ?? new UnitTestInfo[] { };
        }
    }
}
