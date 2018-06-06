$ErrorActionPreference = 'Stop'; # stop on all errors
$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$binDir = Get-BinRoot

$uninstalled = $false

Stop-Service Docker.WatchForwarder  -ErrorAction SilentlyContinue

if(Test-Path $binDir\Docker.WatchForwarder.exe) {
    & "$binDir\Docker.WatchForwarder.exe" uninstall
}

if(Test-Path $binDir) {
    Remove-Item $binDir -Recurse
}

$uninstalled = $true

