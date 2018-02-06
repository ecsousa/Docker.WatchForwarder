using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Docker.WatchForwarder
{
    public static class Logger
    {
        private static TextWriter Output = Console.Out;

        public static void Write(string message)
        {
            Output.WriteLine(message);
        }

        public static void Write(string message, params string[] args)
        {
            Output.WriteLine(message, args);
        }

    }
}
