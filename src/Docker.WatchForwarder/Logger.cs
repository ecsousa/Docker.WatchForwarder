using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Docker.WatchForwarder
{
    public static class Logger
    {
        static Logger()
        {
            if(Environment.UserInteractive)
            {
                WriteAction = Console.WriteLine;
                WriteFormatAction = Console.WriteLine;
            }
            else
            {
                var directory = Path.GetDirectoryName(typeof(Logger).Assembly.Location);
                var path = Path.Combine(directory, "watcher.log");

                WriteAction = message => File.AppendAllLines(path, new []{ message });
                WriteFormatAction = (message, args) => File.AppendAllLines(path, new []{ string.Format(message, args) });
            }
        }

        private static Action<string> WriteAction;
        private static Action<string, object[]> WriteFormatAction;

        public static void Write(string message)
        {
            WriteAction(message);
        }

        public static void Write(string message, params object[] args)
        {
            WriteFormatAction(message, args);
        }

    }
}
