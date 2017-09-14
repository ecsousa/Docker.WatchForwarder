using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Docker.WatchForwarder
{
    public class FSWatcher: IDisposable
    {
        private const int DEBOUNCE_WATCHER_MILLISECONDS_TIMEOUT = 500;
        private const int DEBOUNCE_DOCKER_MILLISECONDS_TIMEOUT = 100;

        private string _sourcePath;
        private FileSystemWatcher _watcher;
        private string _containerId;
        private string _containerPath;
        private string _name;
        private HashSet<string> _touchIgnoreList;
        private HashSet<Process> _executingProcess;
        private Dictionary<string, CancellationTokenSource> _delayedTasks;

        public FSWatcher(string containerId, string name, string sourcePath, string containerPath)
        {
            _containerId = containerId;
            _sourcePath = sourcePath;
            _containerPath = containerPath;
            _name = name;

            _watcher = new FileSystemWatcher(_sourcePath);
            _touchIgnoreList = new HashSet<string>();
            _executingProcess = new HashSet<Process>();
            _delayedTasks = new Dictionary<string, CancellationTokenSource>();

            _watcher.Changed += OnFileChanged;
            _watcher.Renamed += OnFileRenamed;
            _watcher.Created += OnFileCreated;
            _watcher.Deleted += OnFileDelated;
            _watcher.IncludeSubdirectories = true;
            _watcher.EnableRaisingEvents = true;

            Console.WriteLine($"Watching {sourcePath} for {name}:{containerPath}");
        }

        private void OnFileDelated(object sender, FileSystemEventArgs e)
        {
            CancelTouch(e.FullPath, null);

            var parentDirectory = Path.GetDirectoryName(e.FullPath);
            while(!Directory.Exists(parentDirectory))
            {
                parentDirectory = Path.GetDirectoryName(parentDirectory);

                if (parentDirectory == null) // Sanity check: went beyond root directory
                    return;
            }

            DebounceTouch(parentDirectory);
        }

        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            DebounceTouch(e.FullPath);
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            CancelTouch(e.OldName, null);

            DebounceTouch(e.FullPath);
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            DebounceTouch(e.FullPath);
        }

        private void DebounceTouch(string fileName)
        {
            var cancellationTokenSource = new CancellationTokenSource();

            CancelTouch(fileName, cancellationTokenSource);

            Task.Run(() => Touch(fileName, cancellationTokenSource.Token));
        }

        private void CancelTouch(string fileName, CancellationTokenSource cancellationTokenSource)
        {
            lock (_delayedTasks)
            {
                if (_delayedTasks.ContainsKey(fileName))
                    _delayedTasks[fileName].Cancel();

                if (cancellationTokenSource != null)
                    _delayedTasks[fileName] = cancellationTokenSource;
                else
                    _delayedTasks.Remove(fileName);
            }
        }

        private async Task Touch(string fileName, CancellationToken cancellationToken)
        {
            var containerFileName = fileName
                .Replace(_sourcePath, _containerPath)
                .Replace("\\", "/");

            lock(_touchIgnoreList)
            {
                if(_touchIgnoreList.Contains(containerFileName))
                {
                    return; // Prevent bouncing events publish from Docker to Windows back to Docker
                }
            }

            await Task.Delay(DEBOUNCE_WATCHER_MILLISECONDS_TIMEOUT, cancellationToken);

            if(cancellationToken.IsCancellationRequested)
                return;

            lock(_delayedTasks)
            {
                if(_delayedTasks.ContainsKey(fileName) && _delayedTasks[fileName].Token == cancellationToken)
                    _delayedTasks.Remove(fileName);
            }

            lock(_touchIgnoreList)
            {
                _touchIgnoreList.Add(containerFileName);
            }

            try
            {
                if(Execute($"touch {containerFileName}"))
                    await Task.Delay(DEBOUNCE_DOCKER_MILLISECONDS_TIMEOUT);
            }
            finally
            {
                lock(_touchIgnoreList)
                {
                    _touchIgnoreList.Remove(containerFileName);
                }
            }

        }

        private bool Execute(string containerCommand)
        {
            var psi = new ProcessStartInfo();
            psi.UseShellExecute = false;
            psi.FileName = "docker";
            psi.Arguments = $"exec {_containerId} {containerCommand}";

            Console.WriteLine($"Executing: {psi.FileName} {psi.Arguments}");

            var process = Process.Start(psi);
            lock(_executingProcess)
            {
                _executingProcess.Add(process);
            }

            try
            {
                process.WaitForExit();

                return process.ExitCode == 0;
            }
            finally
            {
                lock (_executingProcess)
                {
                    _executingProcess.Remove(process);
                }
            }

        }


        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            _watcher.EnableRaisingEvents = false;
            Console.WriteLine($"Stopped watching {_sourcePath} for {_name}:{_containerPath}");

            lock(_executingProcess)
            {
                foreach(var process in _executingProcess)
                    process.WaitForExit();
            }

            if (!disposedValue)
            {
                if (disposing)
                {
                    _watcher.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~FSWatcher() {
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
