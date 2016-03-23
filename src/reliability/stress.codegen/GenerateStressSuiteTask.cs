// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using stress.codegen.utils;
using System.Diagnostics;

namespace stress.codegen
{
    public class GenerateStressSuiteTask : Task
    {
        public bool DebugWaitForInput { get; set; }

        public string Seed { get; set; }

        [Required]
        public string SuiteName { get; set; }

        [Required]
        public string SuitePath { get; set; }

        /// <summary>
        /// Semicolon separated list of paths containing unit test assemblies
        /// </summary>
        [Required]
        public string TestPaths { get; set; }

        /// <summary>
        /// Semicolon separated list of test assembly search strings used to find test assemblies
        /// </summary>
        public string TestSearchStrings { get; set; }

        /// <summary>
        /// Semicolon separated list of paths containing framework assemblies
        /// </summary>
        [Required]
        public string FrameworkPaths { get; set; }

        /// <summary>
        /// Path to the json config file containing the load suite configuration details
        /// </summary>
        [Required]
        public string ConfigPath { get; set; }

        /// <summary>
        /// Path to the cache of previously discovered tests
        /// </summary>
        public string DiscoveryCachePath { get; set; }

        public override bool Execute()
        {
            if (DebugWaitForInput)
            {
                this.Log.LogMessageFromText($"PID:{Process.GetCurrentProcess().Id} Attach debugger now.", MessageImportance.High);

                while (DebugWaitForInput) ;
            }

            try
            {
                CodeGenOutput.Redirect(new TaskLogOutputWriter(this.Log));

                LoadSuiteGenerator suiteGen = new LoadSuiteGenerator();

                suiteGen.GenerateSuite(this.ParseSeed(), this.SuiteName, this.SuitePath, this.ParseTestPaths(), this.ParseSearchStrings(), this.ParseFrameworkPaths(), this.GetSuiteConfig(), this.DiscoveryCachePath);

                return true;
                //return this.Log.HasLoggedErrors;
            }
            catch (Exception e)
            {
                this.Log.LogErrorFromException(e);

                return false;
            }
        }

        private LoadSuiteConfig GetSuiteConfig()
        {
            return LoadSuiteConfig.Deserialize(this.ConfigPath);
        }

        private string[] ParseSearchStrings()
        {
            return (this.TestSearchStrings != null) ? this.TestSearchStrings.Split(';') : null;
        }

        private string[] ParseTestPaths()
        {
            return this.TestPaths.Split(';');
        }

        private string[] ParseFrameworkPaths()
        {
            return this.FrameworkPaths.Split(';');
        }

        private int ParseSeed()
        {
            int seed;

            if (this.Seed == null || !int.TryParse(this.Seed, out seed))
            {
                seed = new Random().Next();
            }

            return seed;
        }

        private class TaskLogOutputWriter : IOutputWriter
        {
            private TaskLoggingHelper _logHelper;

            public TaskLogOutputWriter(TaskLoggingHelper logHelper)
            {
                _logHelper = logHelper;
            }

            public void WriteError(string message)
            {
                _logHelper.LogError(message);
            }

            public void WriteInfo(string message)
            {
                _logHelper.LogMessage(message);
            }

            public void WriteWarning(string message)
            {
                _logHelper.LogWarning(message);
            }
        }
    }
}
