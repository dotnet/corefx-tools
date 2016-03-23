// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace stress.codegen
{
    public class LoadTestSourceFileGenerator : ISourceFileGenerator
    {
        private HashSet<string> _includedAliases = new HashSet<string>();

        private List<string> _testNames = new List<string>();

        public LoadTestInfo LoadTest { get; set; }

        public void GenerateSourceFile(LoadTestInfo testInfo)
        {
            this.LoadTest = testInfo;

            string unitTestsClassContentSnippet = this.BuildUnitTestsClassContentSnippet();

            string unitTestInitSnippet = this.BuildUnitTestInitSnippet();

            string externAliasSnippet = this.BuildExternAliasSnippet();

            string testSnippet = this.BuildTestSnippet();

            string source = $@"
{externAliasSnippet}

using System;
using System.Threading;
using stress.execution;
using Xunit;

namespace stress.generated
{{
    public static class UnitTests
    {{
        {unitTestsClassContentSnippet}
    }}             

    public static class LoadTestClass
    {{
        {unitTestInitSnippet}

        {testSnippet}
    }}
}}
    ";

            string srcFilePath = Path.Combine(this.LoadTest.SourceDirectory, "LoadTest.cs");

            File.WriteAllText(srcFilePath, source);

            testInfo.SourceFiles.Add(new SourceFileInfo(srcFilePath, SourceFileAction.Compile));
        }

        private string BuildExternAliasSnippet()
        {
            StringBuilder snippet = new StringBuilder();

            foreach (var alias in this.LoadTest.AssemblyAliases)
            {
                snippet.Append($"extern alias {alias};{Environment.NewLine}");
            }

            return snippet.ToString();
        }

        private string BuildTestSnippet()
        {
            string testSnippet = $@" 
        [Load(""{ this.LoadTest.Duration.ToString()}"")]
        public static void LoadTestMethod(CancellationToken cancelToken)
        {{
            {this.LoadTest.TestPatternType.Name} testPattern = new {this.LoadTest.TestPatternType.Name}();

            testPattern.Initialize(0, g_unitTests);             

            {this.LoadTest.WorkerStrategyType.Name} workerStrategy = new {this.LoadTest.WorkerStrategyType.Name}();

            {this.LoadTest.LoadPatternType.Name} loadPattern = new {this.LoadTest.LoadPatternType.Name}();
            
            loadPattern.WorkerCount = {this.LoadTest.WorkerCount};

            loadPattern.Execute(testPattern, workerStrategy, cancelToken);
        }}";

            return testSnippet;
        }

        private string BuildUnitTestsClassContentSnippet()
        {
            StringBuilder classContentSnippet = new StringBuilder();

            int i = 0;

            foreach (var uTest in this.LoadTest.UnitTests)
            {
                string testName = $"UT{i++.ToString("X")}";

                _testNames.Add(testName);

                _includedAliases.Add(uTest.AssemblyAlias);


                string testWrapper = $@" 
        [Fact]
        public static void {testName}()
        {{
            {BuildUnitTestMethodContentSnippet(uTest)}
        }}
";
                classContentSnippet.Append(testWrapper);
            }

            return classContentSnippet.ToString();
        }

        private string BuildUnitTestMethodContentSnippet(UnitTestInfo uTest)
        {
            string contentSnippet = uTest.Method.IsStatic ? $"{uTest.QualifiedMethodStr}();" : $"new { uTest.QualifiedTypeStr }().{ uTest.QualifiedMethodStr}();";

            return contentSnippet;
        }

        private string BuildUnitTestInitSnippet()
        {
            StringBuilder arrayContentSnippet = new StringBuilder();

            foreach (var testName in _testNames)
            {
                arrayContentSnippet.Append($@"new UnitTest(UnitTests.{testName}),
            ");
            }

            return $@"
        static UnitTest[] g_unitTests = new UnitTest[] 
        {{
            {arrayContentSnippet}
        }};";
        }
    }
}
