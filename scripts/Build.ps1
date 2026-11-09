param([switch]$SkipTests)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$out=Join-Path $root 'dist'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(-not (Test-Path $compiler)) { throw 'Requires 64-bit Windows and the .NET Framework C# compiler.' }
New-Item $out -ItemType Directory -Force | Out-Null
& $compiler /nologo /warnaserror+ /target:library /optimize+ "/out:$out\BroadcastMicGuard.Core.dll" "$root\src\Recovery.cs" "$root\src\CoreAudio.cs"
if($LASTEXITCODE) { throw 'Core compilation failed' }
& $compiler /nologo /warnaserror+ /target:winexe /optimize+ /platform:anycpu "/out:$out\BroadcastMicResumeGuard.exe" "/reference:$out\BroadcastMicGuard.Core.dll" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "$root\src\Program.cs"
if($LASTEXITCODE) { throw 'Application compilation failed' }
Copy-Item "$root\src\BroadcastMicResumeGuard.exe.config" $out
Copy-Item "$root\scripts\Install.ps1","$root\scripts\Uninstall.ps1","$root\README.md","$root\CONTRIBUTING.md","$root\SECURITY.md","$root\LICENSE" $out
if(-not $SkipTests) {
    & $compiler /nologo /warnaserror+ /target:exe "/out:$out\PolicyTests.exe" "/reference:$out\BroadcastMicGuard.Core.dll" "$root\tests\PolicyTests.cs"
    if($LASTEXITCODE) { throw 'Test compilation failed' }
    & "$out\PolicyTests.exe"
    if($LASTEXITCODE) { throw 'Policy tests failed' }
}
Get-ChildItem $out -File | Where-Object {$_.Name -ne 'SHA256SUMS.txt' -and $_.Name -ne 'PolicyTests.exe'} | ForEach-Object {
    $hash=Get-FileHash $_.FullName -Algorithm SHA256
    "$($hash.Hash.ToLowerInvariant())  $($_.Name)"
} | Set-Content "$out\SHA256SUMS.txt" -Encoding ASCII
Write-Output "Build output: $out"
