// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// 

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace stress.codegen
{
    public class ProgramSourceFileGenerator : ISourceFileGenerator
    {
        public void GenerateSourceFile(LoadTestInfo loadTest)
        {
            string sourceCode = $@"
using System;
using System.Threading;
using System.Threading.Tasks;
using stress.execution;

namespace stress.generated
{{
    public static class Program
    {{
        public static void Main(string[] args)
        {{
            TimeSpan duration = TimeSpan.Parse(""{loadTest.Duration.ToString()}"");
            
            CancellationTokenSource tokenSource = new CancellationTokenSource(duration);
            
            LoadTestClass.LoadTestMethod(tokenSource.Token);
        }}
    }}
}}
    ";

            string srcFilePath = Path.Combine(loadTest.SourceDirectory, "Program.cs");

            File.WriteAllText(srcFilePath, sourceCode);

            loadTest.SourceFiles.Add(new SourceFileInfo(srcFilePath, SourceFileAction.Compile));
        }
    }
}
