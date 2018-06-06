$ErrorActionPreference = 'Stop'; # stop on all errors
$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"

$watcherExe = "$toolsDir\bin\Docker.WatchForwarder.exe"

& $watcherExe install

Start-Service Docker.WatchForwarder

