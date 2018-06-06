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
#if NETFULL
using Topshelf;
#endif


namespace Docker.WatchForwarder
{
    class Program
    {
        static void Main(string[] args)
        {

#if NETFULL
            //while (!System.Diagnostics.Debugger.IsAttached)
            //    Thread.Sleep(500);

            ConfigureSevice();
#else
            ExecuteService();
#endif
        }

#if NETFULL
        static void ConfigureSevice()
        {
            HostFactory.Run(configure =>
            {

                configure.Service<DockerWatcherService>(service =>
                {
                    service.ConstructUsing(name => new DockerWatcherService(name));
                    service.WhenStarted(watcher => watcher.Start());
                    service.WhenStopped(watcher => watcher.Stop());
                });

                configure.SetDescription("Docker FileSystem Watcher Forwarder");
                configure.SetDisplayName("Docker.WatchForwarder");
                configure.SetServiceName("Docker.WatchForwarder");
                configure.RunAsLocalSystem();
            });
        
        }
#endif

        static void ExecuteService()
        {
            var service = new DockerWatcherService("Docker.WatchForwarder");

            service.Start();

            Console.CancelKeyPress += CancelKeyPressed;
            void CancelKeyPressed(object sender, ConsoleCancelEventArgs e)
            {
                service.Stop();
                e.Cancel = true;
            }

            service.WaitWorker();
        }

    }
}
