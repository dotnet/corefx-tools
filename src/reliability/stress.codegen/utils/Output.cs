// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
            lock (s_consoleLock)
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

        private static object s_consoleLock = new object();
    }

    //if TaskLog is
    //outputs to the console with color formatting by default
    public class CodeGenOutput
    {
        private static IOutputWriter s_writer = new ConsoleOutputWriter();

        private CodeGenOutput() { }

        //public static MSBuild.TaskLoggingHelper TaskLog { get; set; }

        public static void Redirect(IOutputWriter writer)
        {
            s_writer = writer;
        }

        public static void Info(string message)
        {
            s_writer.WriteInfo(message);
        }

        public static void Warning(string message)
        {
            s_writer.WriteWarning(message);
        }

        public static void Error(string message)
        {
            s_writer.WriteError(message);
        }
    }
}
