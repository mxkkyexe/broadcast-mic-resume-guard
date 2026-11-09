# Read-only integration check. Sends synthetic power messages only to the observation process.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$exe=Join-Path $root 'dist\BroadcastMicResumeGuard.exe'
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class SmokeWindow {
    public delegate bool Callback(IntPtr handle,IntPtr param);
    [DllImport("user32.dll")] public static extern bool EnumWindows(Callback callback,IntPtr param);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr handle,System.Text.StringBuilder text,int size);
    public static IntPtr FindOwned(uint process) {
        IntPtr result=IntPtr.Zero;
        EnumWindows(delegate(IntPtr handle,IntPtr param) {
            uint owner; GetWindowThreadProcessId(handle,out owner);
            if(owner == process) {
                var text=new System.Text.StringBuilder(512); GetWindowText(handle,text,512);
                Console.WriteLine("Owned window "+handle+": "+text);
                if(text.ToString() == "BroadcastMicResumeGuard.Power") result=handle;
            }
            return true;
        },IntPtr.Zero);
        return result;
    }
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string cls,string title);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
}
'@
$monitor=Start-Process $exe -ArgumentList '--observe' -PassThru
try {
    Start-Sleep -Seconds 3
    if($monitor.HasExited) { throw 'Observation process exited; stop an existing compiled guard before testing.' }
    $window=[SmokeWindow]::FindOwned([uint32]$monitor.Id)
    [uint32]$windowProcess=0
    [void][SmokeWindow]::GetWindowThreadProcessId($window,[ref]$windowProcess)
    if($window -eq [IntPtr]::Zero -or $windowProcess -ne $monitor.Id) { throw 'Cannot verify ownership of the power window.' }
    [void][SmokeWindow]::SendMessage($window,0x218,[IntPtr]4,[IntPtr]::Zero)
    [void][SmokeWindow]::SendMessage($window,0x218,[IntPtr]18,[IntPtr]::Zero)
    [void][SmokeWindow]::SendMessage($window,0x218,[IntPtr]7,[IntPtr]::Zero)
    Start-Sleep -Seconds 7
    $process=Get-Process -Id $monitor.Id
    [pscustomobject]@{PID=$process.Id; WorkingSetMB=[math]::Round($process.WorkingSet64/1MB,1); CPUSeconds=$process.CPU; VisibleMainWindow=($process.MainWindowHandle -ne 0)}
    $log=Join-Path $env:LOCALAPPDATA 'BroadcastMicResumeGuard\events.log'
    Get-Content $log -Tail 12
} finally {
    if(-not $monitor.HasExited) {
        Start-Process $exe -ArgumentList '--stop' -Wait
        if(-not $monitor.WaitForExit(5000)) { throw 'Observation process did not stop cleanly.' }
    }
}
'Smoke check complete. Synthetic messages test the window handler, not actual Windows sleep/resume delivery. No volume writes were enabled.'
