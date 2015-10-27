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
            string file = File.Exists("/etc/issue") ? "stress.sh" : "stress.bat"; 

            ProcessStartInfo startInfo = new ProcessStartInfo() { FileName = file, UseShellExecute = true };

            //Process 
            Process p = Process.Start(startInfo);

            p.WaitForExit();

            if(p.ExitCode != 0)
            {
                throw new Exception($"Stress test process exited with non-zero exit code {p.ExitCode}"); // Assert.True(false, string.Format("Stress tests returned error code of {0}.", p.ExitCode));
            }
        }
    }
}
