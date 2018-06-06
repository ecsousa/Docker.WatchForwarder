# Docker.WatchForwarder

This utility will connect to Docker service and watch every folder that has been mapped to
Docker. Whenever a file is change, it will trigger a `chmod` command to it on the Docker
container.

## Install using Chocolatey

1. Make sure you have [Chocolatey](https://chocolatey.org/) installed.
2. Run the command in a elevated prompt:

```
choco install docker.watchforwarder
```

## Why?

When you are using Linux Containers on [Docker for Windows](https://www.docker.com/docker-windows)
and you change a Windows file that has been mapped to a container, it doesn't have a file
system event on the container. This can be annoying in some situation; for instance:

* Using Docker to run a Web site that uses some live reload technique, and code the site
  on Windows host.
* Using Docker to run a .NET Core backend that uses
  [.NET Watcher](https://www.nuget.org/packages/Microsoft.DotNet.Watcher.Tools/).
* Some other container solution that relies on file system events.
