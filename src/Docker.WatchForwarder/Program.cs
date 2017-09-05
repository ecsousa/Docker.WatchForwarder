using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Runtime.InteropServices;

namespace Docker.WatchForwarder
{
    class Program
    {
        static void Main(string[] args)
        {
            var watchers =  ListRunningContainer()
                .SelectMany(id => InspectContainer(id))
                .Select(map => new FSWatcher(map.id, map.name, map.source, map.destination))
                .ToArray();

            Console.WriteLine("Press CTRL-C to exit.");

            var exitSignal = new ManualResetEvent(false);

            Console.CancelKeyPress += CancelKeyPressed;

            void CancelKeyPressed(object sender, ConsoleCancelEventArgs e)
            {
                Console.WriteLine("Exiting...");

                foreach(var watcher in watchers)
                    watcher.Dispose();

                exitSignal.Set();
                e.Cancel = true;
            }

            exitSignal.WaitOne();

            Console.WriteLine("Done!");
        }

        private static IEnumerable<string> ListRunningContainer() {

            var psi = new ProcessStartInfo();
            psi.FileName = "docker";
            psi.Arguments = "ps -q";
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;

            var process = Process.Start(psi);

            var line = process.StandardOutput.ReadLine();
            while(line != null)
            {
                yield return line;
                line = process.StandardOutput.ReadLine();
            }
        }

        private static IEnumerable<(string id, string name, string source, string destination)> InspectContainer(string id)
        {

            var psi = new ProcessStartInfo();
            psi.FileName = "docker";
            psi.Arguments = $"inspect {id}";
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;

            var process = Process.Start(psi);

            var payload = JToken.Parse(process.StandardOutput.ReadToEnd());

            var name = payload[0]["Name"].ToString().Substring(1);

            var mountPoints = payload[0]["Mounts"]
                .Where(item => item["Type"].ToString() == "bind");
            
            foreach(var mount in mountPoints)
            {
                var source = mount["Source"].ToString();
                var destination = mount["Destination"].ToString();

                if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    source = $"{source.Substring(1, 1)}:{source.Substring(2)}";

                if(Directory.Exists(source))
                {
                    source = $"{source}/";
                    destination = $"{destination}/";

                    yield return (id, name, source, destination);
                }

            }


        }

    }
}
