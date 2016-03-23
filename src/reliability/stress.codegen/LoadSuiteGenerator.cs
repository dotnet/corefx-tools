// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using stress.codegen.utils;
using stress.execution;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace stress.codegen
{
    public class LoadSuiteGenerator
    {
        private UnitTestSelector _unitTestSelector;

        public void GenerateSuite(int seed, string suiteName, string outputPath, string[] testPaths, string[] searchPatterns, string[] hintPaths, LoadSuiteConfig config, string cachePath = null)
        {
            int suiteTestCount = 0;

            _unitTestSelector = new UnitTestSelector();

            _unitTestSelector.Initialize(seed, testPaths, searchPatterns, hintPaths, cachePath);

            for (int iConfig = 0; iConfig < config.LoadTestConfigs.Count; iConfig++)
            {
                var loadTestConfig = config.LoadTestConfigs[iConfig];

                for (int i = 0; i < loadTestConfig.TestCount; i++)
                {
                    var loadTestInfo = new LoadTestInfo()
                    {
                        TestName = suiteName + "_" + suiteTestCount.ToString("X4"),
                        Duration = loadTestConfig.Duration,
                        LoadPatternType = typeof(StaticLoadPattern),                          //Type.GetType(loadTestConfig.LoadPattern),
                        TestPatternType = typeof(RandomTestPattern),                          //Type.GetType(loadTestConfig.TestPattern),
                        WorkerStrategyType = typeof(DedicatedThreadWorkerStrategy),           //Type.GetType(loadTestConfig.WorkerStrategy),
                        WorkerCount = loadTestConfig.NumWorkers,
                        EnvironmentVariables = loadTestConfig.EnvironmentVariables,
                        SuiteConfig = config,
                    };

                    loadTestInfo.SourceDirectory = Path.Combine(outputPath, iConfig.ToString("00") + "_" + loadTestInfo.Duration.TotalHours.ToString("00.##") + "hr", loadTestInfo.TestName);
                    loadTestInfo.UnitTests = _unitTestSelector.NextUnitTests(loadTestConfig.NumTests).ToArray();

                    this.GenerateTestSources(loadTestInfo);
                    CodeGenOutput.Info($"Generated Load Test: {loadTestInfo.TestName}");
                    suiteTestCount++;
                }
            }
        }

        private void GenerateTestSources(LoadTestInfo loadTest)
        {
            Directory.CreateDirectory(loadTest.SourceDirectory);

            CopyUnitTestAssemblyRefsAsync(loadTest);

            new LoadTestSourceFileGenerator().GenerateSourceFile(loadTest);

            new ProgramSourceFileGenerator().GenerateSourceFile(loadTest);

            // Check whether Linux/Mac or Windows...we don't actually want to do both here.
            new ExecutionFileGeneratorWindows().GenerateSourceFile(loadTest);
            new ExecutionFileGeneratorLinux().GenerateSourceFile(loadTest);

            HelixProjectFileGenerator.GenerateProjectFile(loadTest);
        }

        private void CopyUnitTestAssemblyRefsAsync(LoadTestInfo loadTest)
        {
            string refDir = Path.Combine(loadTest.SourceDirectory, "refs");

            Directory.CreateDirectory(refDir);

            foreach (var assmPath in loadTest.UnitTests.Select(t => t.AssemblyPath).Union(loadTest.UnitTests.SelectMany(t => t.ReferenceInfo.ReferencedAssemblies.Select(ra => ra.Path))))
            {
                string destPath = Path.Combine(refDir, Path.GetFileName(assmPath));

                if (!File.Exists(destPath))
                {
                    File.Copy(assmPath, destPath);
                }
                //await FileUtils.CopyDirAsync(Path.GetDirectoryName(assmPath), refDir);
            }
        }
    }
}
