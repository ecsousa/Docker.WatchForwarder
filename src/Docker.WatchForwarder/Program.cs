using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Runtime.InteropServices;
using Docker.DotNet;
using Docker.DotNet.Models;
using System.Threading.Tasks;

namespace Docker.WatchForwarder
{
    class Program
    {
        static void Main(string[] args)
        {
            Task.Run(MainAsync).Wait();
        }

        static async Task MainAsync()
        {
            var dockerClient = new DockerClientConfiguration(new Uri("npipe://./pipe/docker_engine"))
                .CreateClient();

            var monitor = new ContainersMonitor(dockerClient);

            await monitor.Initialize();

            Console.WriteLine("Press CTRL-C to exit.");

            var exitSignal = new ManualResetEvent(false);

            var monitorCancellationSource = new CancellationTokenSource();
            Console.CancelKeyPress += CancelKeyPressed;

            void CancelKeyPressed(object sender, ConsoleCancelEventArgs e)
            {
                Console.WriteLine("Exiting...");

                monitorCancellationSource.Cancel();

                monitor.Dispose();
            }

            await dockerClient.System.MonitorEventsAsync(
                new ContainerEventsParameters(),
                monitor,
                monitorCancellationSource.Token);

        }


    }
}
