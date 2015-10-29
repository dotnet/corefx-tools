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
    public class XUnitTestDiscoverer : ITestDiscoverer
    {
        public UnitTestInfo[] GetTests(TestAssemblyInfo assemblyInfo)
        {
            List<string> assmRefs = new List<string>();

            foreach (var refAssm in assemblyInfo.Assembly.GetReferencedAssemblies().Select(name => name.Name).Where(name => !name.StartsWith("System")))
            {
                assmRefs.Add(refAssm);
            }

            List<UnitTestInfo> tests = new List<UnitTestInfo>();

            if (assemblyInfo != null)
            {
                foreach (var assmClass in assemblyInfo.Assembly.GetTypes())
                {
                    foreach (var method in assmClass.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                    {
                        var attributes = method.GetCustomAttributesData();

                        if (attributes.Any(attr => attr.AttributeType.Name == "FactAttribute" || attr.AttributeType.Name == "TestMethodAttribute"))
                        {
                            UnitTestInfo test = new UnitTestInfo()
                            {
                                AssemblyPath = assemblyInfo.Assembly.Location,
                                ReferenceInfo = assemblyInfo.ReferenceInfo,
                                Class = new TestClassInfo { FullName = assmClass.FullName, IsAbstract = assmClass.IsAbstract, IsGenericType = assmClass.IsGenericType || assmClass.IsGenericTypeDefinition, IsPublic = assmClass.IsPublic, HasDefaultCtor = assmClass.GetConstructor(Type.EmptyTypes) != null },
                                Method = new TestMethodInfo { Name = method.Name, IsAbstract = method.IsAbstract, IsGenericMethodDefinition = method.IsGenericMethodDefinition, IsPublic = method.IsPublic, IsStatic = method.IsStatic, IsVoidReturn = method.ReturnType.Name.ToLowerInvariant() == "void" },
                            };

                            tests.Add(test);
                        }
                    }
                }
            }

            return tests.ToArray();
        }
    }
}
