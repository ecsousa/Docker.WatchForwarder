using Docker.DotNet;
using Docker.DotNet.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Docker.WatchForwarder
{
    public class ContainerWatcher: IProgress<JSONMessage>, IDisposable
    {
        private DockerClient _client;
        private IDictionary<string, IList<FileSystemWatcher>> _watchers = new Dictionary<string, IList<FileSystemWatcher>>();

        public ContainerWatcher(DockerClient client)
        {
            _client = client;
        }

        public async Task Initialize(CancellationToken cancellationToken)
        {
            foreach (var container in await _client.Containers.ListContainersAsync(new ContainersListParameters(), cancellationToken))
            {
                var containerWatchers = CreateWatchersForContainer(container);
                _watchers.Add(container.ID, containerWatchers);
            }
        }

        private IList<FileSystemWatcher> CreateWatchersForContainer(ContainerListResponse container)
        {
            var watchers = new List<FileSystemWatcher>();

            var id = container.ID;
            var name = container.Names
                .Select(n => n.Substring(1))
                .FirstOrDefault();

            foreach (var mount in container.Mounts)
            {
                var source = mount.Source;
                var destination = mount.Destination;

#if !NETFULL
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
#endif
                {
                    if (source.StartsWith("/host_mnt/"))
                        source = $"{source.Substring(10, 1)}:{source.Substring(11)}";
                    else
                        source = $"{source.Substring(1, 1)}:{source.Substring(2)}";
                }

                if (Directory.Exists(source))
                {
                    source = $"{source}/";
                    destination = $"{destination}/";

                    watchers.Add(new FileSystemWatcher(id, name, source, destination));
                }

            }

            return watchers;
        }

        void IProgress<JSONMessage>.Report(JSONMessage value)
        {
            IList<FileSystemWatcher> containerWatchers;
            switch(value.Status)
            {
                case "start":
                    var container = _client.Containers.ListContainersAsync(new ContainersListParameters())
                        .Result
                        .Where(c => c.ID == value.ID)
                        .FirstOrDefault();

                    if (container == null)
                        break;

                    containerWatchers = CreateWatchersForContainer(container);
                    lock (_watchers)
                    {
                        _watchers[value.ID] = containerWatchers;
                    }

                    break;

                case "stop":

                    lock(_watchers)
                    {
                        if (!_watchers.ContainsKey(value.ID))
                            break;

                        containerWatchers = _watchers[value.ID];
                        _watchers.Remove(value.ID);
                    }

                    foreach (var watcher in containerWatchers)
                        watcher.Dispose();

                    break;

            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    lock(_watchers)
                        foreach (var watcher in _watchers.SelectMany(kvp => kvp.Value))
                            watcher.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~ContainersMonitor() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
