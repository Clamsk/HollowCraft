$ErrorActionPreference='Stop'
$nativeRootTask=Split-Path $PSScriptRoot -Parent
. (Join-Path $nativeRootTask 'scripts\native-launcher-process.ps1')
$fixtureTask=Join-Path $nativeRootTask ('artifacts\native-launcher-checks\'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureTask -Force | Out-Null
$stdoutTask=Join-Path $fixtureTask 'stdout.log';$stderrTask=Join-Path $fixtureTask 'stderr.log'
[IO.File]::WriteAllText($stdoutTask,'窗口已找回 / restored',[Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText($stderrTask,'测试失败 / expected failure',[Text.UTF8Encoding]::new($false))
$script:checksTask=0
function CheckNativeTask([bool]$Condition,[string]$Reason){if(-not $Condition){throw $Reason};$script:checksTask++}
function FakeNativeHelperTask([bool]$Exited,[int]$ExitCode) {
    $fakeTask=[pscustomobject]@{HasExited=$Exited;ExitCode=$ExitCode;Disposed=0;Refreshed=0}
    $fakeTask | Add-Member ScriptMethod Refresh {$this.Refreshed=[int]$this.Refreshed+1}
    $fakeTask | Add-Member ScriptMethod Dispose {$this.Disposed=[int]$this.Disposed+1}
    return $fakeTask
}
$stateTask=@{Helper=$null}
CheckNativeTask ($null -eq (Receive-HollowCraftNativeHelperTask $stateTask $stdoutTask $stderrTask)) 'Idle poll must do nothing'
$fakeTask=FakeNativeHelperTask $false 0;$stateTask.Helper=$fakeTask
CheckNativeTask ($null -eq (Receive-HollowCraftNativeHelperTask $stateTask $stdoutTask $stderrTask) -and $stateTask.Helper -eq $fakeTask -and $fakeTask.Disposed -eq 0) 'Running helper must stay owned'
$fakeTask.HasExited=$true;$resultTask=Receive-HollowCraftNativeHelperTask $stateTask $stdoutTask $stderrTask
CheckNativeTask ($resultTask.ExitCode -eq 0 -and $resultTask.Message -eq '窗口已找回 / restored' -and $null -eq $stateTask.Helper -and $fakeTask.Disposed -eq 1) 'Successful result is consumed and UTF8 survives'
CheckNativeTask ($null -eq (Receive-HollowCraftNativeHelperTask $stateTask $stdoutTask $stderrTask) -and $fakeTask.Disposed -eq 1) 'Completed helper must not be disposed twice'
$fakeTask=FakeNativeHelperTask $true 1;$stateTask.Helper=$fakeTask;$resultTask=Receive-HollowCraftNativeHelperTask $stateTask $stdoutTask $stderrTask
CheckNativeTask ($resultTask.ExitCode -eq 1 -and $resultTask.Message -eq '测试失败 / expected failure' -and $null -eq $stateTask.Helper) 'Failed result is consumed exactly once'
$script:reentryStateTask=@{Helper=(FakeNativeHelperTask $true 1)};$script:reentryResultTask='not-polled'
$script:reentryStateTask.Helper | Add-Member ScriptMethod Refresh {$script:reentryResultTask=Receive-HollowCraftNativeHelperTask $script:reentryStateTask '' ''} -Force
$resultTask=Receive-HollowCraftNativeHelperTask $script:reentryStateTask $stdoutTask $stderrTask
CheckNativeTask ($null -eq $script:reentryResultTask -and $resultTask.ExitCode -eq 1 -and $null -eq $script:reentryStateTask.Helper) 'Reentrant callback must never rediscover a completed helper'
$fakeTask=FakeNativeHelperTask $true 9;$stateTask.Helper=$fakeTask;$resultTask=Receive-HollowCraftNativeHelperTask $stateTask '' ''
CheckNativeTask ($resultTask.ExitCode -eq 9 -and $resultTask.Message -match '9') 'Missing stderr still reports an exit status'
$fakeTask=FakeNativeHelperTask $true 0;$fakeTask | Add-Member ScriptMethod Dispose {throw 'dispose failed'} -Force;$stateTask.Helper=$fakeTask;$resultTask=Receive-HollowCraftNativeHelperTask $stateTask $stdoutTask $stderrTask
CheckNativeTask ($resultTask.ExitCode -eq 0 -and $null -eq $stateTask.Helper) 'Cleanup failure cannot poison future polls'
$fakeTask=FakeNativeHelperTask $true 0;$fakeTask.PSObject.Properties.Remove('HasExited');$fakeTask | Add-Member ScriptProperty HasExited {throw 'poll failed'};$stateTask.Helper=$fakeTask;$resultTask=Receive-HollowCraftNativeHelperTask $stateTask $stdoutTask $stderrTask
CheckNativeTask ($resultTask.ExitCode -eq 1 -and $null -eq $stateTask.Helper -and $fakeTask.Disposed -eq 1) 'Polling exceptions consume the failed helper safely'
$selfTask=Get-Process -Id $PID;$recordPathTask=Join-Path $fixtureTask 'process.json'
@{id=$selfTask.Id;path=$selfTask.Path;startedTicks=$selfTask.StartTime.ToUniversalTime().Ticks} | ConvertTo-Json | Set-Content -LiteralPath $recordPathTask -Encoding UTF8
$ownedTask=Get-HollowCraftNativeOwnedProcessTask $recordPathTask $selfTask.Path
CheckNativeTask ($null -ne $ownedTask -and $ownedTask.Id -eq $selfTask.Id) 'Owned process identity must match pid, time and path';$ownedTask.Dispose()
$mismatchTask=$false;try{$null=Get-HollowCraftNativeOwnedProcessTask $recordPathTask (Join-Path $fixtureTask 'other.exe')}catch{$mismatchTask=$true}
CheckNativeTask $mismatchTask 'Path mismatch must reject the process'
@{id=$selfTask.Id;path=$selfTask.Path;startedTicks=($selfTask.StartTime.ToUniversalTime().Ticks-1)} | ConvertTo-Json | Set-Content -LiteralPath $recordPathTask -Encoding UTF8
CheckNativeTask ($null -eq (Get-HollowCraftNativeOwnedProcessTask $recordPathTask $selfTask.Path)) 'Stale start time must not match a reused process id'
Write-Output ('Native launcher checks: '+$script:checksTask+' passed. No GUI or game was launched or closed.')
