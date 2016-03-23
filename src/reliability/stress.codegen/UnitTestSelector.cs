// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json;
using stress.codegen.utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace stress.codegen
{
    public class UnitTestSelector
    {
        private UnitTestInfo[] _candidates;
        private Dictionary<string, List<UnitTestInfo>> _candidateCache;
        private int _candidateIdx;
        private Random _rand;

        public void Initialize(int seed, string[] paths, string[] patterns, string[] hintPaths, string cachePath = null)
        {
            _rand = new Random(seed);

            if (cachePath != null)
            {
                _candidateCache = LoadCacheFromFile(cachePath);
            }

            _candidates = this.FindAllTests(paths, patterns, hintPaths).ToArray();

            if (cachePath != null)
            {
                WriteCacheToFile(cachePath);
            }

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

        private Dictionary<string, List<UnitTestInfo>> LoadCacheFromFile(string path)
        {
            Dictionary<string, List<UnitTestInfo>> cache = null;

            if (File.Exists(path))
            {
                UnitTestInfo[] cachedTests = null;
                try
                {
                    cachedTests = ReadCacheFromFile(path);

                    Dictionary<string, DateTime> assmTimestamp = new Dictionary<string, DateTime>();

                    cache = new Dictionary<string, List<UnitTestInfo>>();

                    foreach (var test in cachedTests)
                    {
                        DateTime currentAssmTimestamp;

                        if (!assmTimestamp.TryGetValue(test.AssemblyPath, out currentAssmTimestamp))
                        {
                            currentAssmTimestamp = File.Exists(test.AssemblyPath) ? File.GetLastWriteTime(test.AssemblyPath) : DateTime.MaxValue;

                            assmTimestamp[test.AssemblyPath] = currentAssmTimestamp;
                        }

                        if (currentAssmTimestamp == test.AssemblyLastModified)
                        {
                            List<UnitTestInfo> assmTests;
                            if (!cache.TryGetValue(test.AssemblyPath, out assmTests))
                            {
                                assmTests = new List<UnitTestInfo>();

                                cache[test.AssemblyPath] = assmTests;
                            }

                            assmTests.Add(test);
                        }
                    }
                }
                catch (Exception e)
                {
                    CodeGenOutput.Warning($"Unable to read test discovery cache file: {path}.\n{e.ToString()}");
                }
            }

            return cache;
        }

        public void WriteCacheToFile(string path)
        {
            try
            {
                // Serialize the RunConfiguration
                JsonSerializer serializer = JsonSerializer.CreateDefault();

                using (FileStream fs = new FileStream(path, FileMode.Create))
                {
                    using (StreamWriter writer = new StreamWriter(fs))
                    {
                        serializer.Serialize(writer, _candidates);
                    }
                }
            }
            catch (Exception e)
            {
                CodeGenOutput.Warning($"Unable to write test discovery cache file: {path}.\n{e.ToString()}");
            }
        }

        public static UnitTestInfo[] ReadCacheFromFile(string path)
        {
            UnitTestInfo[] cache = null;

            // Deserialize the RunConfiguration
            JsonSerializer serializer = JsonSerializer.CreateDefault();

            using (FileStream fs = new FileStream(path, FileMode.Open))
            {
                using (StreamReader reader = new StreamReader(fs))
                {
                    JsonTextReader jReader = new JsonTextReader(reader);

                    // Call the Deserialize method to restore the object's state.
                    cache = serializer.Deserialize<UnitTestInfo[]>(jReader);
                }
            }

            return cache;
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
            List<UnitTestInfo> cachedTests;
            if (_candidateCache != null && _candidateCache.TryGetValue(path, out cachedTests))
            {
                CodeGenOutput.Info($"{path}: {cachedTests.Count} tests discovered from cache");
                return cachedTests.ToArray();
            }
            else
            {
                return FindTests(path, hintPaths);
            }
        }

        private UnitTestInfo[] FindTests(string path, string[] hintPaths)
        {
            var codeGenDllPath = Assembly.GetExecutingAssembly().Location;

            var codegenDir = Path.GetDirectoryName(codeGenDllPath);

            AppDomain loaderDomain = AppDomain.CreateDomain(path, AppDomain.CurrentDomain.Evidence, new AppDomainSetup() { ApplicationBase = codegenDir });

            var loader = (TestAssemblyLoader)loaderDomain.CreateInstanceFromAndUnwrap(codeGenDllPath, typeof(TestAssemblyLoader).FullName);

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

            if (tests.Length > 0)
            {
                CodeGenOutput.Info($"{path}: {tests.Length} tests discovered from assembly");
            }

            return tests;
        }
    }
}
