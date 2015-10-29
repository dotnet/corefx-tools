// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// 

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace stress.codegen
{
    public class TestAssemblyLoader : MarshalByRefObject
    {
        private TestAssemblyInfo _assembly;

        public string AssemblyPath { get; set; }

        public string LoadError { get; set; }

        public string[] HintPaths { get; set; }

        public bool Load(string assemblyPath, string[] hintPaths)
        {
            this.AssemblyPath = assemblyPath;

            this.HintPaths = hintPaths;



            this.LoadError = null;

            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += IsoDomain_ReflectionOnlyAssemblyResolve;

            try
            {
                _assembly = new TestAssemblyInfo() { Assembly = Assembly.ReflectionOnlyLoadFrom(this.AssemblyPath), ReferenceInfo = new TestReferenceInfo() };
            }
            catch (Exception e)
            {
                this.LoadError = e.ToString();
            }

            return this.LoadError == null;
        }

        public UnitTestInfo[] GetTests<TDiscoverer>()
            where TDiscoverer : ITestDiscoverer, new()
        {
            try
            {
                var discoverer = new TDiscoverer();

                return discoverer.GetTests(_assembly);
            }
            catch (Exception e)
            {
                this.LoadError = (this.LoadError ?? string.Empty) + e.ToString();
            }

            return new UnitTestInfo[] { };
        }

        private Assembly IsoDomain_ReflectionOnlyAssemblyResolve(object sender, ResolveEventArgs args)
        {
            Assembly assm = null;
            if (s_loadAttempted.Add(args.Name))
            {
                try
                {
                    assm = Assembly.ReflectionOnlyLoadFrom(args.Name);

                    this.AddTestAssemblyReference(assm);

                    return assm;
                }
                catch
                {
                    assm = ReflectionOnlyAssemblyResolveFromHintPaths(sender, args);

                    this.AddTestAssemblyReference(assm);

                    return assm;
                }
            }
            return null;
        }

        private Assembly ReflectionOnlyAssemblyResolveFromHintPaths(object sender, ResolveEventArgs args)
        {
            if (this.HintPaths != null)
            {
                for (int i = 0; i < this.HintPaths.Length; i++)
                {
                    try
                    {
                        string assmFile = new AssemblyName(args.Name).Name + ".dll";
                        string hintPath = Path.Combine(this.HintPaths[i], assmFile);
                        if (File.Exists(hintPath))
                        {
                            return Assembly.ReflectionOnlyLoadFrom(hintPath);
                        }
                        if (File.Exists(Path.ChangeExtension(hintPath, ".exe")))
                        {
                            return Assembly.ReflectionOnlyLoadFrom(hintPath);
                        }
                    }
                    catch
                    {
                    }
                }
            }

            return null;
        }

        private void AddTestAssemblyReference(Assembly assembly)
        {
            if (assembly.GetName().Name.ToLowerInvariant() != "mscorlib")
            {
                if (IsFrameworkAssembly(assembly))
                {
                    _assembly.ReferenceInfo.FrameworkReferences.Add(assembly.GetName().Name);
                }
                else
                {
                    _assembly.ReferenceInfo.ReferencedAssemblies.Add(new AssemblyReference() { Path = assembly.Location });
                }
            }
        }

        private bool IsFrameworkAssembly(Assembly assembly)
        {
            string assmName = assembly.GetName().Name;

            var attrDataList = assembly.GetCustomAttributesData();

            bool isFxAssm = assmName.StartsWith("System.") && assembly.GetName().Version.ToString() != "999.999.999.999";

            return isFxAssm;
        }


        internal static Dictionary<string, string> g_ResolvedAssemblies = new Dictionary<string, string>();
        private static HashSet<string> s_loadAttempted = new HashSet<string>();
    }
}
