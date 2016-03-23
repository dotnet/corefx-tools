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
