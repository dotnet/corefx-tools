// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using stress.execution;

namespace stress.codegen
{
    public class ExecutionFileGeneratorLinux : ISourceFileGenerator
    {
        /*
         * scriptName - name of the script to be generated, this should include the path
         * testName - the name of the test executable
         * arguments - arguments to be passed to the test executable
         * envVars - dictionary of environment variables and their values
         * host (optional) - needed for hosted runtimes
         */
        public void GenerateSourceFile(LoadTestInfo loadTestInfo)
        {
            string lldbInspectionFileName = "inspectCoreWithLLDB.py";

            string shellScriptPath = Path.Combine(loadTestInfo.SourceDirectory, "stress.sh");

            using (TextWriter stressScript = new StreamWriter(shellScriptPath, false))
            {
                // set the line ending for shell scripts
                stressScript.NewLine = "\n";

                stressScript.WriteLine("!# /bin/sh");
                stressScript.WriteLine();
                stressScript.WriteLine();

                stressScript.WriteLine("# stress script for {0}", loadTestInfo.TestName);
                stressScript.WriteLine();
                stressScript.WriteLine();

                stressScript.WriteLine("# environment section");
                // first take care of the environment variables
                foreach (KeyValuePair<string, string> kvp in loadTestInfo.EnvironmentVariables)
                {
                    stressScript.WriteLine("export {0}={1}", kvp.Key, kvp.Value);
                }
                stressScript.WriteLine();
                stressScript.WriteLine();

                // The default limit for coredumps on Linux and Mac is 0 and needs to be reset to allow core dumps to be created
                stressScript.WriteLine("# The default limit for coredumps on Linux and Mac is 0 and this needs to be reset to allow core dumps to be created");
                stressScript.WriteLine("echo calling [ulimit -c unlimited]");
                stressScript.WriteLine("ulimit -c unlimited");
                // report the current limits (in theory this should get into the test log)
                stressScript.WriteLine("echo calling [ulimit -a]");
                stressScript.WriteLine("ulimit -a");
                stressScript.WriteLine();
                stressScript.WriteLine();

                // Prepare the test execution line
                string testCommandLine = loadTestInfo.TestName + ".exe";

                // If there is a host then prepend it to the test command line
                if (!String.IsNullOrEmpty(loadTestInfo.SuiteConfig.Host))
                {
                    testCommandLine = loadTestInfo.SuiteConfig.Host + " " + testCommandLine;
                    // If the command line isn't a full path or ./ for current directory then add it to ensure we're using the host in the current directory
                    if ((!loadTestInfo.SuiteConfig.Host.StartsWith("/")) && (!loadTestInfo.SuiteConfig.Host.StartsWith("./")))
                    {
                        testCommandLine = "./" + testCommandLine;
                    }
                }
                stressScript.WriteLine("# test execution");

                stressScript.WriteLine("echo calling [{0}]", testCommandLine);
                stressScript.WriteLine(testCommandLine);
                // Save off the exit code
                stressScript.WriteLine("export _EXITCODE=$?");

                stressScript.WriteLine("echo test exited with ExitCode: $_EXITCODE");

                // Check the return code
                stressScript.WriteLine("if [ $_EXITCODE != 0 ]");

                stressScript.WriteLine("then");

                //This is a temporary hack workaround for the fact that the process exits before the coredump file is completely written
                //We need to replace this with a more hardened way to guaruntee that we don't zip and upload before the coredump is available
                stressScript.WriteLine("  echo Work item failed waiting for coredump...");
                stressScript.WriteLine("  sleep 2m");

                stressScript.WriteLine("  echo zipping work item data for coredump analysis");

                stressScript.WriteLine($"  echo EXEC:  $HELIX_PYTHONPATH $HELIX_SCRIPT_ROOT/zip_script.py $HELIX_WORKITEM_ROOT/../{loadTestInfo.TestName}.zip $HELIX_WORKITEM_ROOT $HELIX_WORKITEM_ROOT/execution $HELIX_WORKITEM_ROOT/core_root");

                stressScript.WriteLine($"  $HELIX_PYTHONPATH $HELIX_SCRIPT_ROOT/zip_script.py -zipFile $HELIX_WORKITEM_ROOT/../{loadTestInfo.TestName}.zip $HELIX_WORKITEM_ROOT $HELIX_WORKITEM_ROOT/execution $HELIX_WORKITEM_ROOT/core_root");

                stressScript.WriteLine($"  echo uploading coredump zip to $HELIX_RESULTS_CONTAINER_URI{loadTestInfo.TestName}.zip analysis");

                stressScript.WriteLine($"  echo EXEC: $HELIX_PYTHONPATH $HELIX_SCRIPT_ROOT/upload_result.py -result $HELIX_WORKITEM_ROOT/../{loadTestInfo.TestName}.zip -result_name {loadTestInfo.TestName}.zip -upload_client_type Blob");

                stressScript.WriteLine($"  $HELIX_PYTHONPATH $HELIX_SCRIPT_ROOT/upload_result.py -result $HELIX_WORKITEM_ROOT/../{loadTestInfo.TestName}.zip -result_name {loadTestInfo.TestName}.zip -upload_client_type Blob");

                stressScript.WriteLine("fi");
                ////            stressScript.WriteLine("zip -r {0}.zip .", testName);
                ////            stressScript.WriteLine("else");
                ////            stressScript.WriteLine("  echo JRS - Test Passed. Report the pass.");
                //stressScript.WriteLine("fi");
                //stressScript.WriteLine();
                //stressScript.WriteLine();

                // exit the script with the return code
                stressScript.WriteLine("exit $_EXITCODE");
            }

            // Add the shell script to the source files
            loadTestInfo.SourceFiles.Add(new SourceFileInfo(shellScriptPath, SourceFileAction.Binplace));


            var shimAssmPath = Assembly.GetAssembly(typeof(StressTestShim)).Location;
            var shimAssm = Path.GetFileName(shimAssmPath);
            string shimRefPath = Path.Combine(loadTestInfo.SourceDirectory, shimAssm);

            File.Copy(shimAssmPath, shimRefPath);

            loadTestInfo.SourceFiles.Add(new SourceFileInfo(shimAssmPath, SourceFileAction.Binplace));


            // Generate the python script, figure out if the run script is being generated into
            // a specific directory, if so then generate the LLDB python script there as well
            GenerateLLDBPythonScript(lldbInspectionFileName, loadTestInfo);
        }

        // The reason for the spacing and begin/end blocks is that python relies on whitespace instead of things
        // like being/ends each nested block increases the spaces by 2
        public void GenerateLLDBPythonScript(string scriptName, LoadTestInfo loadTestInfo)
        {
            // If the application is hosted then the debuggee is the host, otherwise it is the test exe
            string debuggee = String.IsNullOrEmpty(loadTestInfo.SuiteConfig.Host) ? loadTestInfo.TestName + ".exe" : loadTestInfo.SuiteConfig.Host;

            // the name of the core file (should be in the current directory)
            string coreFileName = "core";

            // LastEvent.txt will contain the ClrStack, native callstack and last exception (if I can get it)
            string lastEventFile = "LastEvent.txt";

            // LLDBError.txt will contain any error messages from failures to LLDB
            string lldbErrorFile = "LLDBError.txt";

            // Threads.txt will contain full native callstacks for each threads (equivalent of bt all)
            string threadsFile = "Threads.txt";

            string scriptNameWithPath = Path.Combine(loadTestInfo.SourceDirectory, scriptName);

            using (TextWriter lldbScript = new StreamWriter(scriptNameWithPath, false))
            {
                // set the line ending for linux/mac
                lldbScript.NewLine = "\n";
                lldbScript.WriteLine("import lldb");
                // Create the debugger object
                lldbScript.WriteLine("debugger = lldb.SBDebugger.Create()");

                // Create the return object. This contains the return informaton (success/failure, output or error text etc) from the debugger call
                lldbScript.WriteLine("retobj = lldb.SBCommandReturnObject()");

                // Load the SOS plugin
                lldbScript.WriteLine("debugger.GetCommandInterpreter().HandleCommand(\"plugin load libsosplugin.so\", retobj)");

                // Create the target 
                lldbScript.WriteLine("target = debugger.CreateTarget('{0}')", debuggee);
                // If the target was created successfully
                lldbScript.WriteLine("if target:");
                {
                    // Load the core
                    lldbScript.WriteLine("  process = target.LoadCore('{0}')", coreFileName);
                    {
                        // 
                        lldbScript.WriteLine("  debugger.GetCommandInterpreter().HandleCommand(\"sos ClrStack\", retobj)");
                        lldbScript.WriteLine("  if retobj.Succeeded():");
                        {
                            lldbScript.WriteLine("    LastEventFile = open('{0}', 'w')", lastEventFile);
                            lldbScript.WriteLine("    LastEventFile.write(retobj.GetOutput())");
                            lldbScript.WriteLine("    thread = process.GetSelectedThread()");
                            lldbScript.WriteLine(@"    LastEventFile.write('\n'.join(str(frame) for frame in thread))");
                            lldbScript.WriteLine("    LastEventFile.close()");
                        }
                        lldbScript.WriteLine("  else:");
                        {
                            lldbScript.WriteLine("    LLDBErrorFile = open('{0}', 'w')", lldbErrorFile);
                            lldbScript.WriteLine("    LLDBErrorFile.write(retobj.GetError())");
                            lldbScript.WriteLine("    LLDBErrorFile.close()");
                        }
                        lldbScript.WriteLine("  ThreadsFile = open('{0}', 'w')", threadsFile);
                        lldbScript.WriteLine("  for thread in process:");
                        {
                            lldbScript.WriteLine(@"    ThreadsFile.write('Thread %s:\n' % str(thread.GetThreadID()))");
                            lldbScript.WriteLine("    for frame in thread:");
                            {
                                lldbScript.WriteLine(@"      ThreadsFile.write(str(frame)+'\n')");
                            }
                        }
                        lldbScript.WriteLine("  ThreadsFile.close()");
                    }
                }
            }

            // Add the python script to the source files
            loadTestInfo.SourceFiles.Add(new SourceFileInfo(scriptNameWithPath, SourceFileAction.Binplace));
        }
    }
    public class ExecutionFileGeneratorWindows : ISourceFileGenerator
    {
        public void GenerateSourceFile(LoadTestInfo loadTestInfo)// (string scriptName, string testName, Dictionary<string, string> envVars, string host = null)
        {
            string batchScriptPath = Path.Combine(loadTestInfo.SourceDirectory, "stress.bat");
            using (TextWriter stressScript = new StreamWriter(batchScriptPath, false))
            {
                stressScript.WriteLine("@echo off");
                stressScript.WriteLine("REM stress script for " + loadTestInfo.TestName);
                stressScript.WriteLine();
                stressScript.WriteLine();

                stressScript.WriteLine("REM environment section");
                // first take care of the environment variables
                foreach (KeyValuePair<string, string> kvp in loadTestInfo.EnvironmentVariables)
                {
                    stressScript.WriteLine("set {0}={1}", kvp.Key, kvp.Value);
                }
                stressScript.WriteLine();
                stressScript.WriteLine();

                // Prepare the test execution line
                string testCommandLine = loadTestInfo.TestName + ".exe";

                // If there is a host then prepend it to the test command line
                if (!String.IsNullOrEmpty(loadTestInfo.SuiteConfig.Host))
                {
                    testCommandLine = loadTestInfo.SuiteConfig.Host + " " + testCommandLine;
                }
                stressScript.WriteLine("REM test execution");
                stressScript.WriteLine("echo calling [{0}]", testCommandLine);
                stressScript.WriteLine(testCommandLine);
                // Save off the exit code
                stressScript.WriteLine("set _EXITCODE=%ERRORLEVEL%");
                stressScript.WriteLine("echo test exited with ExitCode: %_EXITCODE%");
                stressScript.WriteLine();
                stressScript.WriteLine();

                //                // Check the return code
                //                stressScript.WriteLine("if %_EXITCODE% EQU 0 goto :REPORT_PASS");
                //                stressScript.WriteLine("REM error processing");
                //                stressScript.WriteLine("echo JRS - Test Failed. Report the failure, call to do the initial dump analysis, zip up the directory and return that along with an event");
                //                stressScript.WriteLine("goto :END");
                //                stressScript.WriteLine();
                //                stressScript.WriteLine();
                //
                //                stressScript.WriteLine(":REPORT_PASS");
                //                stressScript.WriteLine("echo JRS - Test Passed. Report the pass.");
                //                stressScript.WriteLine();
                //                stressScript.WriteLine();

                // exit the script with the exit code from the process
                stressScript.WriteLine(":END");
                stressScript.WriteLine("exit /b %_EXITCODE%");
            }

            // Add the batch script to the source files
            loadTestInfo.SourceFiles.Add(new SourceFileInfo(batchScriptPath, SourceFileAction.Binplace));
        }
    }
}