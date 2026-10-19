param([string]$BossId,[ValidateRange(0,2)][int]$BossLevel=0,[ValidateRange(640,3840)][int]$Width=1280,[ValidateRange(480,2160)][int]$Height=720)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'native-launcher-process.ps1')
$nativeRootTask=Split-Path $PSScriptRoot -Parent
$configPathTask=Join-Path $nativeRootTask 'hollowcraft.native.local.json'
if(-not(Test-Path -LiteralPath $configPathTask)){throw 'Run Install HollowCraft Native.cmd first.'}
$configTask=Get-Content -LiteralPath $configPathTask -Raw -Encoding UTF8 | ConvertFrom-Json
if($configTask.schema -ne 1 -or $configTask.backend -ne 'standalone'){throw 'Invalid native installation configuration.'}
$gameTask=Join-Path $nativeRootTask 'runtime\Hollow Knight Native'
$executableTask=Join-Path $gameTask 'hollow_knight.exe'
$recordPathTask=Join-Path $nativeRootTask 'runtime\hollow-knight-native-process.json'
New-Item -ItemType Directory -Path (Join-Path $nativeRootTask 'runtime') -Force | Out-Null
try {$nativeLaunchLockTask=[IO.File]::Open((Join-Path $nativeRootTask 'runtime\native-start.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)}
catch [IO.IOException] {Write-Output '启动请求正在处理，请稍候 / Another launch request is being processed.';return}
try {
$existingTask=Get-HollowCraftNativeOwnedProcessTask $recordPathTask $executableTask
if($null -ne $existingTask) {
    try {
        if($BossId){throw '普通冒险或录制已在运行。请先点击“关闭独立版”，关闭游戏后再选择新的 Boss / Close the existing native game before starting a new recording.'}
        if(Show-HollowCraftNativeGameTask $existingTask.Id){Write-Output '已找回正在运行的空洞窗口 / Existing Hollow Knight window restored.'}
        else {Write-Output '空洞已在运行，窗口仍在初始化。请稍后再点击继续冒险 / Hollow Knight is already starting. Try Play again shortly.'}
        return
    }finally{$existingTask.Dispose()}
}
if(-not(Test-Path -LiteralPath $executableTask)){throw 'Native game files are missing. Run Install HollowCraft Native.cmd.'}
$env:HOLLOWCRAFT_BACKEND='standalone'
$env:HOLLOWCRAFT_SETTINGS=Join-Path $nativeRootTask 'hollowcraft.native.settings.json'
$env:HOLLOWCRAFT_SAVE_ROOT=Join-Path $nativeRootTask 'runtime\hk-native-saves'
$env:HOLLOWCRAFT_BOSS_REQUEST=$null;$env:HOLLOWCRAFT_BOSS_CATALOG=$null;$env:HOLLOWCRAFT_BOSS_STATUS=$null;$env:HOLLOWCRAFT_RECORD_SESSION=$null
if($BossId) {
    $catalogTask=Join-Path $nativeRootTask 'release\bosses.json'
    $bossTask=(Get-Content -LiteralPath $catalogTask -Raw -Encoding UTF8 | ConvertFrom-Json).items | Where-Object id -eq $BossId | Select-Object -First 1
    if(-not $bossTask){throw 'Unknown boss id.'}
    if(-not $bossTask.godhome){$BossLevel=0}
    $env:HOLLOWCRAFT_RECORD_SESSION=[Guid]::NewGuid().ToString('N')
    $env:HOLLOWCRAFT_SAVE_ROOT=Join-Path $nativeRootTask ('runtime\hk-native-boss-saves\'+$env:HOLLOWCRAFT_RECORD_SESSION)
    $env:HOLLOWCRAFT_BOSS_CATALOG=$catalogTask
    $env:HOLLOWCRAFT_BOSS_REQUEST=Join-Path $nativeRootTask 'runtime\native-boss-request.json'
    $env:HOLLOWCRAFT_BOSS_STATUS=Join-Path $nativeRootTask 'runtime\native-boss-status.json'
    @{schema=1;session=$env:HOLLOWCRAFT_RECORD_SESSION;request=[Guid]::NewGuid().ToString('N');action='boss';boss=$BossId;level=$BossLevel} | ConvertTo-Json | Set-Content -LiteralPath $env:HOLLOWCRAFT_BOSS_REQUEST -Encoding UTF8
}
New-Item -ItemType Directory -Path $env:HOLLOWCRAFT_SAVE_ROOT -Force | Out-Null
$env:HOLLOWCRAFT_AUTOSTART='0';$env:HOLLOWCRAFT_SMOKE='0';$env:HOLLOWCRAFT_CAPTURE='0'
$logTask=Join-Path $nativeRootTask 'runtime\hollowcraft-native.log'
$processTask=Start-Process -FilePath $executableTask -WorkingDirectory $gameTask -ArgumentList @('-logFile',('"'+$logTask+'"'),'-screen-fullscreen','0','-screen-width',$Width,'-screen-height',$Height) -WindowStyle Normal -PassThru
@{id=$processTask.Id;path=$executableTask;startedTicks=$processTask.StartTime.ToUniversalTime().Ticks;backend='standalone';session=$env:HOLLOWCRAFT_RECORD_SESSION;saveRoot=$env:HOLLOWCRAFT_SAVE_ROOT} | ConvertTo-Json | Set-Content -LiteralPath $recordPathTask -Encoding UTF8
Write-Output '空洞已启动 / HollowCraft Native started. A/D 移动 · 空格跳跃 · E 背包 · F 副手。'
}finally{$nativeLaunchLockTask.Dispose()}
