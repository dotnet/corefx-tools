using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace stress.execution
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class LoadAttribute : Attribute
    {
        public LoadAttribute(string duration)
        {

        }

        public string Duration { get; private set; }
    }
}
