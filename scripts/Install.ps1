[CmdletBinding()]
param([switch]$NoStart)
$ErrorActionPreference='Stop'
$existing=Get-ScheduledTask -TaskName 'Broadcast Mic Resume Guard' -ErrorAction SilentlyContinue
if($existing) { throw 'A legacy Broadcast Mic Resume Guard task exists. Uninstall that version first to avoid running two guards.' }
$source=$PSScriptRoot
if(-not (Test-Path "$source\BroadcastMicResumeGuard.exe")) { throw 'Run this installer from the built distribution folder.' }
$destination=Join-Path $env:LOCALAPPDATA 'Programs\BroadcastMicResumeGuard'
New-Item $destination -ItemType Directory -Force | Out-Null
$old=Join-Path $destination 'BroadcastMicResumeGuard.exe'
if(Test-Path $old) {
    $stopping=Start-Process $old -ArgumentList '--stop' -PassThru -Wait
    if($stopping.ExitCode) { throw 'Could not request shutdown of previous guard.' }
    Start-Sleep -Seconds 2
}
foreach($file in @('BroadcastMicResumeGuard.exe','BroadcastMicGuard.Core.dll','BroadcastMicResumeGuard.exe.config','Uninstall.ps1','README.md','CONTRIBUTING.md','SECURITY.md','LICENSE','SHA256SUMS.txt')) {
    $path=Join-Path $source $file
    if([IO.Path]::GetFullPath($path) -ne [IO.Path]::GetFullPath((Join-Path $destination $file))) { Copy-Item $path $destination -Force }
}
$shell=New-Object -ComObject WScript.Shell
$link=$shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Startup')) 'Broadcast Mic Resume Guard.lnk'))
$link.TargetPath=Join-Path $destination 'BroadcastMicResumeGuard.exe'
$link.WorkingDirectory=$destination
$link.Description='Restore the Broadcast microphone pre-sleep level once after resume, respecting mute.'
$link.Save()
if(-not $NoStart) { Start-Process $link.TargetPath }
"Installed for your user at $destination. Starts at login. No administrator privileges required."
