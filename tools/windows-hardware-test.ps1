<#
.SYNOPSIS
  Real-hardware test of the port on Windows (run from an elevated PowerShell).

  Stops the L-Connect services, runs lconnectd against the real TL controller / LCD using a COPY of your
  L-Connect settings, walks through lighting, LCD and fan changes (you watch the fans), then restores the
  original state and restarts L-Connect. Nothing in the L-Connect install directory is modified.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\windows-hardware-test.ps1
#>
param(
	[string]$LConnectDir = 'C:\Program Files\Lian-Li\L-Connect 3',
	[string]$Dotnet = "$env:LOCALAPPDATA\dotnet\dotnet.exe",
	[switch]$NoPause
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
	throw 'Run this from an elevated (Administrator) PowerShell.'
}

$work = Join-Path $env:TEMP ('lconnect-hwtest-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force $work | Out-Null
$env:DOTNET_ROOT = Split-Path $Dotnet
$env:LCONNECT_DATA_DIR = Join-Path $work 'data'
$env:LCONNECT_SOCKET = Join-Path $work 'lconnect.sock'
$env:LCONNECT_HIDAPI = Join-Path $LConnectDir 'hidapi.dll'
$env:LCONNECT_HID_LOG = Join-Path $work 'hid.log'
Remove-Item Env:LCONNECT_SIMULATE -ErrorAction SilentlyContinue

Write-Host "Building into $work\bin ..."
& $Dotnet build "$repo\src\LConnect.Daemon" -c Release -o "$work\bin" -v q -nologo | Out-Null
& $Dotnet build "$repo\src\LConnect.Cli" -c Release -o "$work\bin" -v q -nologo | Out-Null
$ctl = Join-Path $work 'bin\lconnectctl.exe'
$daemon = Join-Path $work 'bin\lconnectd.exe'

function Step([string]$text) {
	Write-Host "`n== $text" -ForegroundColor Cyan
	if (-not $NoPause) { Read-Host 'Press Enter to continue' | Out-Null }
}

$services = 'LConnectServiceWatcher', 'LConnectService'
$startTypes = @{}
foreach ($s in $services) { $startTypes[$s] = (Get-Service $s).StartType }
$task = Get-ScheduledTask -TaskName 'L-Connect 3' -ErrorAction SilentlyContinue
$proc = $null
try {
	Write-Host 'Stopping L-Connect ...'
	foreach ($s in $services) { Set-Service $s -StartupType Manual; Stop-Service $s -Force }
	if ($task) { Disable-ScheduledTask -TaskName 'L-Connect 3' | Out-Null }
	Get-Process -Name 'L-Connect 3' -ErrorAction SilentlyContinue | Stop-Process -Force
	Start-Sleep 2

	& $ctl probe
	& $ctl assets import --from $LConnectDir | Select-Object -Last 1
	& $ctl import --from (Join-Path $LConnectDir 'appdata') | Select-Object -Last 1

	Step 'Starting lconnectd with your current settings (lighting + LCD should look like under L-Connect)'
	$proc = Start-Process $daemon -PassThru -NoNewWindow -RedirectStandardError (Join-Path $work 'daemon.log')
	Start-Sleep 8
	& $ctl status

	Step 'Lighting: Rainbow, full brightness'
	& $ctl tl lighting --port 2 --mode Rainbow --speed 75 --brightness 100
	Step 'Lighting: Meteor magenta/green, counter-clockwise'
	& $ctl tl lighting --port 2 --mode Meteor --speed 50 --brightness 100 --direction LeftOrCounterClockwise --colors ff0080,00ff00
	Step 'LCD: built-in animation'
	$anim = (& $ctl lcd animations | Select-Object -Index 1).Trim()
	& $ctl lcd show --port 2 --animation $anim
	Step 'LCD: CPU load gauge (style 2)'
	& $ctl lcd show --port 2 --sensor CPULoad --style 2 --bg 00a0ff
	Step 'LCD: back to your GPU temperature gauge, fan to HighSpeed'
	& $ctl lcd show --port 2 --sensor GPUTemp --style 0 --bg 0000ff
	& $ctl tl fan --port 2 --mode HighSpeed --source GPU
	Start-Sleep 5
	& $ctl status
	Step 'Done. Stopping lconnectd and restoring L-Connect'
}
finally {
	if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
	Get-Process -Name msedge -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -match 'lconnect' } | Stop-Process -Force -ErrorAction SilentlyContinue
	foreach ($s in $services) { Set-Service $s -StartupType $startTypes[$s] }
	Start-Service LConnectService; Start-Service LConnectServiceWatcher
	if ($task) { Enable-ScheduledTask -TaskName 'L-Connect 3' | Out-Null }
	Write-Host "`nL-Connect restored. Logs: $work (daemon.log, hid.log)" -ForegroundColor Green
}
