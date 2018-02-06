using Docker.DotNet;
using Docker.DotNet.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Docker.WatchForwarder
{
    public class WatcherService
    {
        private string name;
        public ManualResetEvent StoppedSignal { get; set; }
        private bool Stopping = false;
        private Task WorkerTask;
        private CancellationTokenSource MonitorCancellationSource;

        public WatcherService(string name)
        {
            this.name = name;
        }

        public bool Start()
        {
            StoppedSignal = new ManualResetEvent(false);
            Stopping = false;
            WorkerTask = Task.Run(WorkerMethod);
            MonitorCancellationSource = new CancellationTokenSource();
            return true;
        }

        public bool Stop()
        {
            Logger.Write("Disconnecting from Docker...");
            Stopping = true;
            MonitorCancellationSource.Cancel();
            StoppedSignal.WaitOne();
            Logger.Write("Stopped!");
            return true;
        }

        private async Task WorkerMethod()
        {
            while(!MonitorCancellationSource.IsCancellationRequested)
            {
                var dockerClient = new DockerClientConfiguration(GetDockerEndpoint())
                    .CreateClient();

                var monitor = new ContainersMonitor(dockerClient);

                try
                {
                    await monitor.Initialize(MonitorCancellationSource.Token);
                }
                catch(UnauthorizedAccessException)
                {
                    Logger.Write("Access to Docker Denied!");
                    throw;
                }
                catch(Exception e)
                {
                    Logger.Write("Could not connect to Docker. Retrying in 3 seconds...");
                    await Task.Delay(3000, MonitorCancellationSource.Token);
                    continue;
                }

                Logger.Write("Connected to Docker! Awaiting for events...");

                var cancelTaskCompletionSource = new TaskCompletionSource<object>();

                ThreadPool.RegisterWaitForSingleObject(
                    MonitorCancellationSource.Token.WaitHandle,
                    (o, timeout) => { cancelTaskCompletionSource.SetResult(null); },
                    null,
                    -1,
                    true);

                var monitorTask = dockerClient.System.MonitorEventsAsync(
                    new ContainerEventsParameters(),
                    monitor,
                    MonitorCancellationSource.Token);

                await Task.WhenAny(monitorTask, cancelTaskCompletionSource.Task);

                Logger.Write("Diconnected from docker.");
            }

            StoppedSignal?.Set();
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
    }
}
