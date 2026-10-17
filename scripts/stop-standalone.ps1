$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'native-launcher-process.ps1')
$nativeRootTask=Split-Path $PSScriptRoot -Parent
$recordPathTask=Join-Path $nativeRootTask 'runtime\hollow-knight-native-process.json'
if(-not(Test-Path -LiteralPath $recordPathTask)){return}
$expectedTask=Join-Path $nativeRootTask 'runtime\Hollow Knight Native\hollow_knight.exe'
$processTask=Get-HollowCraftNativeOwnedProcessTask $recordPathTask $expectedTask
if($null -eq $processTask){Write-Output '独立版没有运行 / Native HollowCraft is not running.';return}
try {
    $null=Show-HollowCraftNativeGameTask $processTask.Id
    $processTask.Refresh()
    if(-not $processTask.CloseMainWindow()){throw '请在游戏窗口中退出空洞 / Close Hollow Knight from its game window.'}
    Write-Output '已发送正常关闭请求；请完成游戏中的保存提示 / Close requested. Finish any in-game save dialog.'
}finally{$processTask.Dispose()}
