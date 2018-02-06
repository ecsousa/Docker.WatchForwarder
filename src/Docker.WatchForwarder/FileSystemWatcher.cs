using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Docker.WatchForwarder
{
    public class FileSystemWatcher : IDisposable
    {
        private const int DEBOUNCE_WATCHER_MILLISECONDS_TIMEOUT = 500;

        private string _sourcePath;
        private System.IO.FileSystemWatcher _watcher;
        private string _containerId;
        private string _containerPath;
        private string _name;
        private HashSet<Process> _executingProcess;
        private Dictionary<string, CancellationTokenSource> _delayedTasks;

        public FileSystemWatcher(string containerId, string name, string sourcePath, string containerPath)
        {
            _containerId = containerId;
            _sourcePath = sourcePath;
            _containerPath = containerPath;
            _name = name;

            _watcher = new System.IO.FileSystemWatcher(_sourcePath);
            _executingProcess = new HashSet<Process>();
            _delayedTasks = new Dictionary<string, CancellationTokenSource>();

            _watcher.Changed += OnFileChanged;
            _watcher.Renamed += OnFileRenamed;
            _watcher.Created += OnFileCreated;
            _watcher.Deleted += OnFileDelated;
            _watcher.IncludeSubdirectories = true;
            _watcher.EnableRaisingEvents = true;

            Logger.Write($"Watching {sourcePath} for {name}:{containerPath}");
        }

        private void OnFileDelated(object sender, FileSystemEventArgs e)
        {
            CancelTouch(e.FullPath, null);
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
            var containerFileName = TranslateFileName(fileName);

            await Task.Delay(DEBOUNCE_WATCHER_MILLISECONDS_TIMEOUT, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
                return;

            lock (_delayedTasks)
            {
                if (_delayedTasks.ContainsKey(fileName) && _delayedTasks[fileName].Token == cancellationToken)
                    _delayedTasks.Remove(fileName);
            }

            var permission = await Execute($"stat -c%a {containerFileName}");

            if (permission.success)
            {
                if(permission.output.Length < 3)
                {
                    Logger.Write($"Could not get permission from stat's return: {permission.output}");
                    return;
                }

                var chmodResult = await Execute($"chmod {permission.output.Substring(0, 3)} {containerFileName}");

                if(!chmodResult.success)
                {
                    Logger.Write($"chmod failed: {chmodResult.error}");
                }
            }
            else
            {
                Logger.Write($"Error getting permissions of {containerFileName}");
                Logger.Write(permission.error);
            }
        }

        private string TranslateFileName(string hostFileName)
        {
            return hostFileName
                .Replace(_sourcePath, _containerPath)
                .Replace("\\", "/")
                .Replace(" ", "\\ ")
                .Replace("*", "\\*");
        }

        private async Task<(bool success, string output, string error)> Execute(string containerCommand)
        {
            var psi = new ProcessStartInfo();
            psi.UseShellExecute = false;
            psi.FileName = "docker";
            psi.Arguments = $"exec {_containerId} {containerCommand}";
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;

            Logger.Write($"Executing: {psi.FileName} {psi.Arguments}");

            var process = Process.Start(psi);
            lock (_executingProcess)
            {
                _executingProcess.Add(process);
            }

            try
            {
                var outputTexts = await Task.WhenAll(
                    process.StandardOutput.ReadToEndAsync(),
                    process.StandardError.ReadToEndAsync());

                process.WaitForExit();

                return (process.ExitCode == 0, outputTexts[0], outputTexts[1]);
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
            Logger.Write($"Stopped watching {_sourcePath} for {_name}:{_containerPath}");

            lock (_executingProcess)
            {
                foreach (var process in _executingProcess)
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
}
