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
            try
            {
                Task.Run(MainAsync).Wait();
            }
            catch(AggregateException aggregatedExeption)
            {
                aggregatedExeption.Handle(ex =>
                {
                    if(ex is TimeoutException)
                    {
                        Console.Error.WriteLine("Error connecting to Docker! Is it running?");
                        return true;
                    }

                    return false;
                });
            }
        }

        private static Uri GetDockerEndpoint()
        {
            var dockerHost = Environment.GetEnvironmentVariable("DOCKER_HOST");

            if(string.IsNullOrWhiteSpace(dockerHost))
            {
#if !NETFULL
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
#endif
                    dockerHost = "npipe://./pipe/docker_engine";
#if !NETFULL
                else
                    dockerHost = "unix:///var/run/docker.sock";
#endif
            }

            return new Uri(dockerHost);
        }

        static async Task MainAsync()
        {
            var dockerClient = new DockerClientConfiguration(GetDockerEndpoint())
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

                e.Cancel = true;
            }

            var cancelTaskCompletionSource = new TaskCompletionSource<object>();
            ThreadPool.RegisterWaitForSingleObject(
                monitorCancellationSource.Token.WaitHandle,
                (o , timeout) => { cancelTaskCompletionSource.SetResult(null); },
                null,
                -1,
                true);

            var monitorTask = dockerClient.System.MonitorEventsAsync(
                new ContainerEventsParameters(),
                monitor,
                monitorCancellationSource.Token);

            await Task.WhenAny(monitorTask, cancelTaskCompletionSource.Task);

            Console.WriteLine("Disconnected from Docker.");

        }

    }
}
