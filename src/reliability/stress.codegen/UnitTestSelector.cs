// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// 

using stress.codegen.utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace stress.codegen
{
    public class UnitTestSelector
    {
        private UnitTestInfo[] _candidates;
        private int _candidateIdx;
        private Random _rand;

        public void Initialize(int seed, string[] paths, string[] patterns, string[] hintPaths)
        {
            _rand = new Random(seed);

            _candidates = this.FindAllTests(paths, patterns, hintPaths).ToArray();

            CodeGenOutput.Info($"Discovered {_candidates.Length} unit tests, across {_candidates.Select(t => t.AssemblyPath).Distinct().Count()} assemblies.");
        }

        public IEnumerable<UnitTestInfo> NextUnitTests(int count)
        {
            if (count > _candidates.Length) throw new ArgumentOutOfRangeException("count");

            for (int i = 0; i < count; i++)
            {
                //if this is the first call to NextTests or we've looped though candidate tests shuffle the test list
                if ((_candidateIdx % _candidates.Length) == 0)
                {
                    _candidates = _candidates.OrderBy(t => _rand.NextDouble()).ToArray();
                }

                yield return _candidates[_candidateIdx++ % _candidates.Length];
            }
        }

        private IEnumerable<UnitTestInfo> FindAllTests(string[] paths, string[] patterns, string[] hintPaths)
        {
            foreach (string dir in paths)
            {
                foreach (string searchPattern in patterns)
                {
                    foreach (string assmPath in Directory.EnumerateFiles(dir, searchPattern, SearchOption.AllDirectories))
                    {
                        foreach (UnitTestInfo test in this.GetTests(assmPath, hintPaths))
                        {
                            if (test.IsLoadTestCandidate())
                            {
                                yield return test;
                            }
                        }
                    }
                }
            }
        }

        private UnitTestInfo[] GetTests(string path, string[] hintPaths)
        {
            AppDomain loaderDomain = AppDomain.CreateDomain(path);

            var loader = (TestAssemblyLoader)loaderDomain.CreateInstanceAndUnwrap(Assembly.GetExecutingAssembly().FullName, typeof(TestAssemblyLoader).FullName);

            loader.InitializeLifetimeService();

            HashSet<string> hints = new HashSet<string>(hintPaths) { Path.GetDirectoryName(path) };

            loader.Load(path, hints.ToArray());

            UnitTestInfo[] tests = loader.GetTests<XUnitTestDiscoverer>();

            //if no xunit tests were discovered and the assembly is an exe treat as a standalone exe test
            if ((tests == null || tests.Length == 0) && Path.GetExtension(path).ToLowerInvariant() == ".exe")
            {
                tests = loader.GetTests<StandAloneTestDiscoverer>();
            }

            AppDomain.Unload(loaderDomain);

            if (tests.Length == 0)
            {
                CodeGenOutput.Warning($"{path}:\n {tests.Length} tests discovered");
            }
            else
            {
                CodeGenOutput.Info($"{path}:\n {tests.Length} tests discovered");
            }

            return tests;
        }
    }
}
