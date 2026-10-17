$ErrorActionPreference='Stop'
$nativeRootTask=Split-Path $PSScriptRoot -Parent
$recordPathTask=Join-Path $nativeRootTask 'runtime\hollow-knight-native-process.json'
if(-not(Test-Path -LiteralPath $recordPathTask)){return}
$recordTask=Get-Content -LiteralPath $recordPathTask -Raw | ConvertFrom-Json
$processTask=Get-Process -Id $recordTask.id -ErrorAction SilentlyContinue
if(-not $processTask -or $processTask.StartTime.ToUniversalTime().Ticks -ne $recordTask.startedTicks){return}
$expectedTask=Join-Path $nativeRootTask 'runtime\Hollow Knight Native\hollow_knight.exe'
if($processTask.Path -ne $expectedTask -or $recordTask.path -ne $expectedTask){throw 'Native process ownership mismatch.'}
$null=$processTask.CloseMainWindow()
Write-Output 'Sent a normal close request. If a save dialog is open, finish it in the game window.'
