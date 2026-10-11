param([switch]$Headless,[switch]$GraphicalSmoke)
$ErrorActionPreference='Stop'
$projectTaskRoot=Split-Path $PSScriptRoot -Parent
$gameTestTask=Join-Path $projectTaskRoot 'runtime\Hollow Knight'
$env:HOLLOWCRAFT_SAVE_ROOT=Join-Path $projectTaskRoot 'runtime\hk-test-saves'
$logTask=Join-Path $projectTaskRoot 'runtime\hollowcraft-hk.log'
$argumentsTask=@('-logFile',('"'+$logTask+'"'),'-screen-fullscreen','0','-screen-width','1280','-screen-height','720')
if ($Headless) { $argumentsTask+=@('-batchmode','-nographics'); $env:HOLLOWCRAFT_AUTOSTART='1'; $env:HOLLOWCRAFT_SMOKE='1'; $env:HOLLOWCRAFT_SAVE_ROOT=Join-Path $projectTaskRoot 'runtime\hk-smoke-saves' }
else { $env:HOLLOWCRAFT_AUTOSTART='0'; $env:HOLLOWCRAFT_SMOKE='0' }
if($GraphicalSmoke) {$env:HOLLOWCRAFT_AUTOSTART='1';$env:HOLLOWCRAFT_SMOKE='1';$env:HOLLOWCRAFT_CAPTURE='1';$env:HOLLOWCRAFT_SAVE_ROOT=Join-Path $projectTaskRoot 'runtime\hk-smoke-saves'}
$windowTask=if($GraphicalSmoke) {'Normal'}else{'Hidden'}
$processTask=Start-Process -FilePath (Join-Path $gameTestTask 'hollow_knight.exe') -WorkingDirectory $gameTestTask -ArgumentList $argumentsTask -WindowStyle $windowTask -PassThru
@{id=$processTask.Id;startedTicks=$processTask.StartTime.ToUniversalTime().Ticks;path=(Join-Path $gameTestTask 'hollow_knight.exe')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $projectTaskRoot ('runtime/test-process-'+$processTask.Id+'.json'))
Write-Output "HollowCraft game PID: $($processTask.Id). Log: $logTask"
