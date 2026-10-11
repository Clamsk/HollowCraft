$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'hollowcraft-common.ps1')
$configTask=Get-HollowCraftConfigTask
$projectTaskRoot=Split-Path $PSScriptRoot -Parent
$profileTask=Join-Path $projectTaskRoot 'runtime\minecraft'
$gameTestTask=Join-Path $projectTaskRoot 'runtime\Hollow Knight'
$bridgeHashTask=(Get-FileHash -LiteralPath (Join-Path $projectTaskRoot 'minecraft\build\libs\hollowcraft-minecraft-0.1.0.jar') -Algorithm SHA256).Hash
# Prevent duplicate worlds and bridge-port conflicts without touching unrelated game processes.
$reuseMinecraftTask=$false
foreach($componentTask in @('minecraft','hollow-knight')) {
    $recordPathTask=Join-Path $projectTaskRoot ("runtime\$componentTask-process.json")
    if(Test-Path -LiteralPath $recordPathTask) {
        $recordTask=Get-Content -LiteralPath $recordPathTask -Raw | ConvertFrom-Json
        $existingTask=Get-Process -Id $recordTask.id -ErrorAction SilentlyContinue
        if($existingTask -and $existingTask.StartTime.ToUniversalTime().Ticks -eq $recordTask.startedTicks) {
            $expectedPathTask=if($componentTask -eq 'minecraft') {$configTask.javaPath}else{Join-Path $gameTestTask 'hollow_knight.exe'}
            if($existingTask.Path -ne $expectedPathTask) {throw 'HollowCraft process ownership mismatch.'}
            if($componentTask -eq 'minecraft') {
                if($recordTask.bridgeSha256 -ne $bridgeHashTask) {throw 'Minecraft backend uses an older or unrecorded build. Run Stop HollowCraft.cmd, then Start HollowCraft.cmd to load the updated mod.'}
                $reuseMinecraftTask=$true
            }else{throw 'HollowCraft is already running. Close the previous game window before launching again.'}
        }
    }
}
if(-not $reuseMinecraftTask -and (Test-NetConnection -ComputerName 127.0.0.1 -Port 31674 -InformationLevel Quiet -WarningAction SilentlyContinue)) {
    throw 'Port 31674 is already in use. Close the previous HollowCraft Minecraft window first.'
}
if(-not(Test-Path -LiteralPath (Join-Path $projectTaskRoot 'artifacts\HollowCraft\Assembly-CSharp.dll'))) {throw 'Run Install HollowCraft.cmd first.'}
& (Join-Path $PSScriptRoot 'prepare-hk-test.ps1') -GameRoot $configTask.gameRoot
if(-not $reuseMinecraftTask) {
    & (Join-Path $PSScriptRoot 'prepare-mc-test.ps1') -MinecraftRoot $configTask.minecraftRoot
    $mcTask=Start-Process -FilePath $configTask.javaPath -WorkingDirectory $profileTask -ArgumentList ('@"'+(Join-Path $profileTask 'launch.args')+'"') -WindowStyle Hidden -RedirectStandardOutput (Join-Path $profileTask 'launcher.stdout.log') -RedirectStandardError (Join-Path $profileTask 'launcher.stderr.log') -PassThru
    @{id=$mcTask.Id;path=$configTask.javaPath;startedTicks=$mcTask.StartTime.ToUniversalTime().Ticks;bridgeSha256=$bridgeHashTask} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $projectTaskRoot 'runtime\minecraft-process.json')
}
$env:HOLLOWCRAFT_SAVE_ROOT=Join-Path $projectTaskRoot 'runtime\hk-test-saves'
$env:HOLLOWCRAFT_AUTOSTART='0'; $env:HOLLOWCRAFT_SMOKE='0';$env:HOLLOWCRAFT_CAPTURE='0'
$logTask=Join-Path $projectTaskRoot 'runtime\hollowcraft-hk.log'
$hkTask=Start-Process -FilePath (Join-Path $gameTestTask 'hollow_knight.exe') -WorkingDirectory $gameTestTask -ArgumentList @('-logFile',('"'+$logTask+'"'),'-screen-fullscreen','0','-screen-width','1280','-screen-height','720') -PassThru
@{id=$hkTask.Id;path=$hkTask.Path;startedTicks=$hkTask.StartTime.ToUniversalTime().Ticks} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $projectTaskRoot 'runtime\hollow-knight-process.json')
Write-Output 'HollowCraft started. Keep Minecraft running and enter Hollow Knight. A/D move, Space jump, Shift crouch. F9 gives a creative combat kit.'
