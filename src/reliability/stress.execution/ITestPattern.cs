using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace stress.execution
{
    public interface ITestPattern
    {
        UnitTest GetNextTest();
    }
}
