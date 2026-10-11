param([switch]$Smoke)
$ErrorActionPreference='Stop'
$projectTaskRoot=Split-Path $PSScriptRoot -Parent
$profileTask=Join-Path $projectTaskRoot 'runtime\minecraft'
if($Smoke) {$profileTask=Join-Path $projectTaskRoot 'runtime\minecraft-smoke'}
$processTask=Start-Process -FilePath 'C:\Program Files\Microsoft\jdk-21.0.10.7-hotspot\bin\java.exe' -WorkingDirectory $profileTask -ArgumentList ('@"'+(Join-Path $profileTask 'launch.args')+'"') -WindowStyle Hidden -RedirectStandardOutput (Join-Path $profileTask 'launcher.stdout.log') -RedirectStandardError (Join-Path $profileTask 'launcher.stderr.log') -PassThru
@{id=$processTask.Id;startedTicks=$processTask.StartTime.ToUniversalTime().Ticks;path='C:\Program Files\Microsoft\jdk-21.0.10.7-hotspot\bin\java.exe'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $projectTaskRoot ('runtime/test-process-'+$processTask.Id+'.json'))
Write-Output "HollowCraft Minecraft PID: $($processTask.Id). Log: $profileTask\logs\latest.log"
