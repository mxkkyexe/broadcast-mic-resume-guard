$ErrorActionPreference='Stop'
$destination=Join-Path $env:LOCALAPPDATA 'Programs\BroadcastMicResumeGuard'
$exe=Join-Path $destination 'BroadcastMicResumeGuard.exe'
if(Test-Path $exe) { Start-Process $exe -ArgumentList '--stop' -Wait; Start-Sleep -Seconds 2 }
$shortcut=Join-Path ([Environment]::GetFolderPath('Startup')) 'Broadcast Mic Resume Guard.lnk'
if(Test-Path $shortcut) { Remove-Item -LiteralPath $shortcut }
# Explicit file allowlist; never recursively delete a caller-supplied directory.
foreach($name in @('BroadcastMicResumeGuard.exe','BroadcastMicGuard.Core.dll','BroadcastMicResumeGuard.exe.config','README.md','CONTRIBUTING.md','SECURITY.md','LICENSE','SHA256SUMS.txt','Uninstall.ps1')) {
    $file=Join-Path $destination $name
    if(Test-Path $file) { Remove-Item -LiteralPath $file }
}
'Startup disabled and installed files removed. Diagnostic logs retained in %LOCALAPPDATA%\BroadcastMicResumeGuard. Microphone settings left unchanged.'
