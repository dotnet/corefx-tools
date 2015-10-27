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

        public LoadTestInfo LoadTest { get; set; }

        public void GenerateSourceFile(LoadTestInfo testInfo)
        {
            this.LoadTest = testInfo;

            string unitTestInitSnippet = this.BuildUnitTestInitSnippet();

            string externAliasSnippet = this.BuildExternAliasSnippet();

            string testSnippet = this.BuildTestSnippet();

            string source = $@"
{externAliasSnippet}

using System;
using System.Threading;
using stress.execution;

namespace stress.generated
{{
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


        private string BuildUnitTestInitSnippet()
        {
            StringBuilder arrayContentSnippet = new StringBuilder();

            StringBuilder instanceCreationSnippet = new StringBuilder();
            
            int instIdx = 0;

            foreach (var uTest in this.LoadTest.UnitTests)
            {
                this._includedAliases.Add(uTest.AssemblyAlias);
                
                if (!uTest.Method.IsStatic)
                {
                    instanceCreationSnippet.Append($@"static {uTest.QualifiedTypeStr} inst{instIdx.ToString("X4")} = new {uTest.QualifiedTypeStr}();
        ");
                    //here we wrap all method calls in an anonymous method to handle cases where the method is not void returning
                    //it is possible that this could be smarter to only do this for non-void returning methods
                    arrayContentSnippet.Append($@"new UnitTest(() => {{ inst{instIdx++.ToString("X4")}.{uTest.QualifiedMethodStr}(); }}),
                                ");
                }
                else
                {
                    //here we wrap all method calls in an anonymous method to handle cases where the method is not void returning
                    //it is possible that this could be smarter to only do this for non-void returning methods
                    arrayContentSnippet.Append($@"new UnitTest(() => {{ {uTest.QualifiedMethodStr}(); }}),
                                ");
                }
            }

            return $@"{instanceCreationSnippet}
        static UnitTest[] g_unitTests = new UnitTest[] 
                        {{
                            {arrayContentSnippet}
                        }};";
        }
    }
}
