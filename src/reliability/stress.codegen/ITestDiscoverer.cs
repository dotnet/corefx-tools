using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace stress.codegen
{

    public interface ITestDiscoverer
    {
        UnitTestInfo[] GetTests(TestAssemblyInfo assembly);
    }

    public interface ISourceFileGenerator
    {
        void GenerateSourceFile(LoadTestInfo loadTest);
    }
}
