using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace stress.codegen.utils
{
    public interface IOutputWriter
    {
        void WriteInfo(string message);

        void WriteWarning(string message);

        void WriteError(string message);
    }


    public class ConsoleOutputWriter : IOutputWriter
    {


        public void WriteInfo(string message)
        {
            WriteToConsole(message);
        }

        public void WriteWarning(string message)
        {
            WriteToConsole(message, ConsoleColor.Yellow);

        }

        public void WriteError(string message)
        {
            WriteToConsole(message, ConsoleColor.Red);
        }

        private static void WriteToConsole(string message, ConsoleColor? color = null)
        {
            lock(g_ConsoleLock)
            {
                var origFgColor = Console.ForegroundColor;

                if (color.HasValue)
                {
                    Console.ForegroundColor = color.Value;
                }

                Console.WriteLine(message);

                Console.ForegroundColor = origFgColor;
            }
        }

        private static object g_ConsoleLock = new object();
    }

    //if TaskLog is
    //outputs to the console with color formatting by default
    public class CodeGenOutput
    {
        private static IOutputWriter g_writer = new ConsoleOutputWriter();

        private CodeGenOutput() { }
        
        //public static MSBuild.TaskLoggingHelper TaskLog { get; set; }

        public static void Redirect(IOutputWriter writer)
        {
            g_writer = writer;
        }

        public static void Info(string message)
        {
            g_writer.WriteInfo(message);
        }

        public static void Warning(string message)
        {
            g_writer.WriteWarning(message);
        }

        public static void Error(string message)
        {
            g_writer.WriteError(message);
        }
        
    }
}
