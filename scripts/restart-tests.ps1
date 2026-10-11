param([int]$MinecraftId,[int]$HollowKnightId,[ValidateSet('pointer','adventure','environment','campaign','containers','baseline','boss','shop','choice','controls')][string]$Kind='environment')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'stop-tests.ps1') -MinecraftId $MinecraftId -HollowKnightId $HollowKnightId
$stamp=Get-Date -Format yyyyMMdd-HHmmss
$evidence=Join-Path $taskRoot ('runtime/hk-smoke-saves/run-'+$stamp)
New-Item -ItemType Directory -Path $evidence -Force|Out-Null
foreach($file in @('hollowcraft-smoke.json','failure-snapshot.json','hollowcraft-preview.png','hollowcraft-inventory.png')) {$source=Join-Path $taskRoot ('runtime/hk-smoke-saves/'+$file);if(Test-Path -LiteralPath $source){Copy-Item -LiteralPath $source -Destination $evidence}}
Copy-Item -LiteralPath (Join-Path $taskRoot 'runtime/hollowcraft-hk.log') -Destination (Join-Path $evidence 'hollow-knight.log')
Copy-Item -LiteralPath (Join-Path $taskRoot 'runtime/minecraft-smoke/logs/latest.log') -Destination (Join-Path $evidence 'minecraft.log')
$old=Join-Path $taskRoot 'runtime/minecraft-smoke/saves/HollowCraft';$archive=Join-Path $taskRoot ('runtime/minecraft-smoke/saves/Archive-'+$stamp)
if([IO.Path]::GetFullPath($old).StartsWith($taskRoot+'\') -and [IO.Path]::GetFullPath($archive).StartsWith($taskRoot+'\')) {Move-Item -LiteralPath $old -Destination $archive}
& (Join-Path $PSScriptRoot 'prepare-hk-test.ps1')
& (Join-Path $PSScriptRoot 'prepare-mc-test.ps1') -Smoke
& (Join-Path $PSScriptRoot 'start-mc-test.ps1') -Smoke
$env:HOLLOWCRAFT_POINTER_ONLY=if($Kind -eq 'pointer'){'1'}else{'0'}
$env:HOLLOWCRAFT_ADVENTURE_SMOKE=if($Kind -eq 'adventure'){'1'}else{'0'}
$env:HOLLOWCRAFT_ENVIRONMENT_SMOKE=if($Kind -eq 'environment'){'1'}else{'0'}
$env:HOLLOWCRAFT_CAMPAIGN_SMOKE=if($Kind -eq 'campaign'){'1'}else{'0'}
$env:HOLLOWCRAFT_CONTAINER_SMOKE=if($Kind -eq 'containers'){'1'}else{'0'}
$env:HOLLOWCRAFT_BOSS_SMOKE=if($Kind -eq 'boss'){'1'}else{'0'}
$env:HOLLOWCRAFT_SHOP_SMOKE=if($Kind -eq 'shop'){'1'}else{'0'}
$env:HOLLOWCRAFT_CHOICE_SMOKE=if($Kind -eq 'choice'){'1'}else{'0'}
$env:HOLLOWCRAFT_CONTROLS_SMOKE=if($Kind -eq 'controls'){'1'}else{'0'}
& (Join-Path $PSScriptRoot 'start-hk-test.ps1') -GraphicalSmoke
