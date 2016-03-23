// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace stress.execution
{
    public class StressTestShim
    {
        private ITestOutputHelper _output;

        public StressTestShim(ITestOutputHelper output)
        {
            _output = output;

            _outbuff = new OutputBuffer(1024 * 200, output);
        }

        [Fact]
        public void ShellExecuteStressTest()
        {
            //determin if we are on a unix/linux system
            string file = Path.Combine(Directory.GetCurrentDirectory(), File.Exists("/etc/issue") ? "stress.sh" : "stress.bat");

            ProcessStartInfo testProc;

            if (File.Exists("/etc/issue"))
            {
                _output.WriteLine("Setting script file permissions");

                _output.WriteLine($"EXEC: chmod 777 {file}");

                ProcessStartInfo startInfo = new ProcessStartInfo() { FileName = "chmod", Arguments = $"777 {file}", UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };

                Process p = Process.Start(startInfo);

                p.WaitForExit();

                _output.WriteLine($"Setting permissions returned: {p.ExitCode}");

                if (p.ExitCode != 0)
                {
                    throw new Exception($"Setting shell script permissions failed with non-zero exit code {p.ExitCode}"); // Assert.True(false, string.Format("Stress tests returned error code of {0}.", p.ExitCode));
                }

                testProc = new ProcessStartInfo() { FileName = "bash", Arguments = file, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            }
            else
            {
                testProc = new ProcessStartInfo() { FileName = file, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            }

            var envVars = Environment.GetEnvironmentVariables();

            //pass environment variables onto the child process
            foreach (string envVarName in envVars.Keys)
            {
                if (!testProc.Environment.ContainsKey(envVarName))
                {
                    _output.WriteLine($"SETTING VARIABLE: {envVarName}={envVars[envVarName]}");

                    testProc.Environment.Add(envVarName, (string)envVars[envVarName]);
                }
            }

            _output.WriteLine($"EXEC: {file}");

            //Process 
            Process p2 = Process.Start(testProc);

            Task logout = LogProcessOutputAsync(p2.StandardOutput);

            Task logerr = LogProcessOutputAsync(p2.StandardError);

            p2.WaitForExit();

            if (p2.ExitCode != 0)
            {
                Task.WhenAll(logout, logerr).GetAwaiter().GetResult();

                _outbuff.FlushToConsole();

                throw new Exception($"Stress test process exited with non-zero exit code {p2.ExitCode}"); // Assert.True(false, string.Format("Stress tests returned error code of {0}.", p.ExitCode));
            }
        }

        public async Task LogProcessOutputAsync(StreamReader stream)
        {
            string s;

            while ((s = await stream.ReadLineAsync()) != null)
            {
                _outbuff.Write(s);
            }
        }

        private OutputBuffer _outbuff;

        private class OutputBuffer
        {
            private static object s_bufferlock = new object();

            private int _curIx = -1;
            private int _size;
            private ITestOutputHelper _output;
            private char[] _buff;

            public OutputBuffer(int size, ITestOutputHelper output)
            {
                _buff = new char[size];
                _size = size;
                _output = output;
            }

            public void Write(string str)
            {
                lock (s_bufferlock)
                {
                    for (int i = 0; i < str.Length; i++)
                    {
                        Write(str[i]);
                    }

                    Write('\n');
                }
            }

            public void Write(char val)
            {
                lock (s_bufferlock)
                {
                    _buff[++_curIx % _size] = val;
                }
            }


            public void FlushToConsole()
            {
                lock (s_bufferlock)
                {
                    if (_curIx >= _size)
                    {
                        _output.WriteLine("");

                        _output.WriteLine("--- OUTPUT TRUNCATED ---");

                        _output.WriteLine("");
                    }

                    StringBuilder builder = new StringBuilder();

                    int endIx = (_curIx < _size) ? _curIx : _curIx + _size;

                    for (int ix = (_curIx < _size) ? 0 : _curIx; ix < endIx; ix++)
                    {
                        builder.Append(_buff[ix % _size]);
                    }

                    _output.WriteLine(builder.ToString());

                    //reset the buffer
                    _curIx = -1;
                }
            }
        }
    }
}
