using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace stress.xunitshim
{
    public class StressTestShim
    {
        [Fact]
        public void ShellExecuteStressTest()
        {
            string file = (int)Environment.OSVersion.Platform < 4 ? "stress.bat" : "stress.sh";
            ProcessStartInfo startInfo = new ProcessStartInfo() { FileName = file, UseShellExecute = true };

            //Process 
            Process p = Process.Start(startInfo);

            p.WaitForExit();

            if(p.ExitCode != 0)
            {
                Assert.True(false, string.Format("Stress tests returned error code of {0}.", p.ExitCode));
            }
        }
    }
}
