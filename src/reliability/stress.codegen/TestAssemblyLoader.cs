// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

                foreach (var refName in _assembly.Assembly.GetReferencedAssemblies())
                {
                    Assembly.ReflectionOnlyLoad(refName.FullName);
                }
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
            if (!s_loaded.TryGetValue(args.Name, out assm))
            {
                try
                {
                    assm = Assembly.ReflectionOnlyLoadFrom(args.Name);

                    this.AddTestAssemblyReference(assm);

                    s_loaded[args.Name] = assm;

                    return assm;
                }
                catch
                {
                    assm = ReflectionOnlyAssemblyResolveFromHintPaths(sender, args);
                    if (assm != null)
                    {
                        this.AddTestAssemblyReference(assm);

                        s_loaded[args.Name] = assm;
                    }
                    return assm;
                }
            }
            return assm;
        }

        private Assembly ReflectionOnlyAssemblyResolveFromHintPaths(object sender, ResolveEventArgs args)
        {
            if (this.HintPaths != null)
            {
                for (int i = 0; i < this.HintPaths.Length; i++)
                {
                    try
                    {
                        string assmDllFile = new AssemblyName(args.Name).Name + ".dll";
                        string assmExeFile = new AssemblyName(args.Name).Name + ".exe";
                        string hintPath = Directory.EnumerateFiles(this.HintPaths[i], assmDllFile, SearchOption.AllDirectories).FirstOrDefault() ?? Directory.EnumerateFiles(this.HintPaths[i], assmExeFile, SearchOption.AllDirectories).FirstOrDefault();

                        if (hintPath != null)
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
                    _assembly.ReferenceInfo.FrameworkReferences.Add(new AssemblyReference() { Path = assembly.Location, Version = assembly.GetName().Version.ToString() });
                }
                else
                {
                    _assembly.ReferenceInfo.ReferencedAssemblies.Add(new AssemblyReference() { Path = assembly.Location, Version = assembly.GetName().Version.ToString() });
                }
            }
        }

        private bool IsFrameworkAssembly(Assembly assembly)
        {
            string assmName = assembly.GetName().Name;

            var attrDataList = assembly.GetCustomAttributesData();

            bool isFxAssm = assmName.StartsWith("System.") && assembly.GetName().Version.ToString() != "999.999.999.999" && !s_knownTestRefs.Contains(assmName);

            return isFxAssm;
        }


        internal static Dictionary<string, string> g_ResolvedAssemblies = new Dictionary<string, string>();
        private static Dictionary<string, Assembly> s_loaded = new Dictionary<string, Assembly>();
        private static HashSet<string> s_knownTestRefs = new HashSet<string>(new string[] { "System.Xml.RW.XmlReaderLib" });
    }
}
