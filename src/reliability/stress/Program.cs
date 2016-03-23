// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable 3016

using stress.codegen;
using stress.execution;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace stress.console
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            MultiFunctionCmdArgs argParser = new MultiFunctionCmdArgs();

            argParser.AddFunction<GenTestsArgs>("createsuite", GenTests);

            argParser.InvokeFunctionWithArgs(args);
        }

        public static int GenTests(GenTestsArgs args)
        {
            LoadSuiteGenerator suiteGen = new LoadSuiteGenerator();

            int seed = args.Seed == 0 ? new Random().Next() : args.Seed;

            suiteGen.GenerateSuite(seed, args.SuiteName, args.OutputPath, args.TestPaths, args.FileMasks, args.HintPaths, LoadSuiteConfig.Deserialize(args.ConfigPath));

            return 0;
        }
    }

    internal class GenTestsArgs : CmdArgsBase
    {
        [CmdArg(typeof(string), "n", "name", Required = true, ValueMoniker = "suite_name", Description = "Path to the json test template file describing the tests to be generated")]
        public string SuiteName { get; set; }

        [CmdArg(typeof(string), "c", "config", Required = true, ValueMoniker = "config_path", Description = "Path to the json load suite config file describing the tests to be generated")]
        public string ConfigPath { get; set; }

        [CmdArg(typeof(string), "o", "output", Required = true, ValueMoniker = "output_path", Description = "Path to the directory in which all generated tests will be placed")]
        public string OutputPath { get; set; }

        [CmdArg(typeof(string[]), "t", "testpaths", Required = true, ValueMoniker = "test_path[;test_pathN...]", Description = "Semicolon separated list of paths to test binaries")]
        public string[] TestPaths { get; set; }

        [CmdArg(typeof(string[]), "f", "filemasks", Required = false, Default = new string[] { "*.dll" }, ValueMoniker = "filemask[;filemaskN...]", Description = "Semicolon separated list of file search strings for test binaries")]
        public string[] FileMasks { get; set; }

        [CmdArg(typeof(string[]), "h", "hintpaths", Required = false, Default = new string[] { }, ValueMoniker = "hintpath[;hintpathN...]", Description = "Semicolon separated list of hint paths for test binary references")]
        public string[] HintPaths { get; set; }

        [CmdArg(typeof(int), "s", "seed", Required = false, ValueMoniker = "suite_seed", Description = "The seed used when randomly seleting unite tests for generated load tests")]
        public int Seed { get; set; }
    }
}
