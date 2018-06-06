$ErrorActionPreference = 'Stop'; # stop on all errors
$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"

$uninstalled = $false

Stop-Service Docker.WatchForwarder  -ErrorAction SilentlyContinue

$watcherExe = "$toolsDir\bin\Docker.WatchForwarder.exe"

if(Test-Path $watcherExe) {
    & $watcherExe uninstall
}

$uninstalled = $true

