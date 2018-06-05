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
    public class DockerWatcherService
    {
        private string name;
        public ManualResetEvent StoppedSignal { get; set; }
        private Task WorkerTask;
        private CancellationTokenSource MonitorCancellationSource;

        public DockerWatcherService(string name)
        {
            this.name = name;
        }

        public bool Start()
        {
            StoppedSignal = new ManualResetEvent(false);
            WorkerTask = Task.Run(WorkerMethod);
            MonitorCancellationSource = new CancellationTokenSource();
            return true;
        }

        public bool Stop()
        {
            Logger.Write("Disconnecting from Docker...");
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

                var monitor = new ContainerWatcher(dockerClient);

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
                    Logger.Write("Exception type: {0}", e.GetType().Name);
                    Logger.Write(e.Message);
                    Logger.Write(e.StackTrace);
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
