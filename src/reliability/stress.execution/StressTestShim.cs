// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// 

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace stress.execution
{
    public class StressTestShim
    {
        [Fact]
        public void ShellExecuteStressTest()
        {
            //determin if we are on a unix/linux system
            string file = Path.Combine(Directory.GetCurrentDirectory(), File.Exists("/etc/issue") ? "stress.sh" : "stress.bat");
            
            ProcessStartInfo startInfo = new ProcessStartInfo() { FileName = file, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };

            Console.WriteLine($"EXEC {file}");

            //Process 
            Process p = Process.Start(startInfo);

            Task logout = LogProcessOutputAsync(p.StandardOutput);

            Task logerr = LogProcessOutputAsync(p.StandardError);

            p.WaitForExit();

            if (p.ExitCode != 0)
            {
                Task.WhenAll(logout, logerr).GetAwaiter().GetResult();

                s_outbuff.FlushToConsole();

                throw new Exception($"Stress test process exited with non-zero exit code {p.ExitCode}"); // Assert.True(false, string.Format("Stress tests returned error code of {0}.", p.ExitCode));
            }
        }

        public async Task LogProcessOutputAsync(StreamReader stream)
        {
            string s;

            while ((s = await stream.ReadLineAsync()) != null)
            {
                s_outbuff.Write(s);
            }
        }

        private static OutputBuffer s_outbuff = new OutputBuffer(2000);

        private class OutputBuffer
        {
            private static object s_bufferlock = new object();

            private int _curIx = -1;
            private int _size;
            
            private char[] _buff;

            public OutputBuffer(int size)
            {
                _buff = new char[size];
                _size = size;
            }

            public void Write(string str)
            {
                lock(s_bufferlock)
                {
                    for(int i = 0; i < str.Length; i++)
                    {
                        Write(str[i]);
                    }
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
                        Console.WriteLine("--- OUTPUT TRUNCATED ---");
                    }

                    int endIx = (_curIx < _size) ? _curIx : _curIx + _size;

                    for (int ix = (_curIx < _size) ? 0 : _curIx; ix < endIx; ix++)
                    {
                        Console.Write(_buff[ix % _size]);
                    }

                    //reset the buffer
                    _curIx = -1;
                }
            }
            
            
        }
    }
}
