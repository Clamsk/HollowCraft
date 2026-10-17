# Process/result helpers shared by the launcher and its focused, game-free checks.
function Receive-HollowCraftNativeHelperTask([hashtable]$State,[string]$OutputPath,[string]$ErrorPath) {
    $processTask=$State.Helper
    if($null -eq $processTask){return $null}
    try {
        $exitedTask=$processTask.HasExited
        if($null -eq $exitedTask){throw 'The launcher helper returned no process state.'}
        if(-not $exitedTask){return $null}
    } catch {
        $failureTask=$_.Exception.Message
        $State.Helper=$null
        try {$processTask.Dispose()} catch {}
        return [pscustomobject]@{ExitCode=1;Message=('Cannot read launcher helper: '+$failureTask)}
    }
    # Detach before Refresh, log reads, disposal or any caller UI. Modal/nested
    # message loops must never observe the completed process a second time.
    $State.Helper=$null
    try {
        $processTask.Refresh()
        $exitTask=$processTask.ExitCode
        if($null -eq $exitTask){throw 'The launcher helper returned no exit status.'}
        $logTask=if($exitTask -eq 0){$OutputPath}else{$ErrorPath}
        $messageTask=if($logTask -and (Test-Path -LiteralPath $logTask)){[IO.File]::ReadAllText($logTask,[Text.Encoding]::UTF8).Trim()}else{''}
        if(-not $messageTask){$messageTask=if($exitTask -eq 0){'完成 / Ready'}else{'操作失败，退出码 / Exit code: '+$exitTask}}
        return [pscustomobject]@{ExitCode=[int]$exitTask;Message=$messageTask}
    } catch {
        return [pscustomobject]@{ExitCode=1;Message=$_.Exception.Message}
    } finally {try {$processTask.Dispose()} catch {}}
}

function Get-HollowCraftNativeOwnedProcessTask([string]$RecordPath,[string]$ExpectedPath) {
    if(-not(Test-Path -LiteralPath $RecordPath)){return $null}
    $recordTask=Get-Content -LiteralPath $RecordPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if(-not $recordTask.id -or -not $recordTask.startedTicks){return $null}
    $processTask=Get-Process -Id ([int]$recordTask.id) -ErrorAction SilentlyContinue
    if($null -eq $processTask){return $null}
    if($processTask.StartTime.ToUniversalTime().Ticks -ne [long]$recordTask.startedTicks){$processTask.Dispose();return $null}
    $expectedTask=[IO.Path]::GetFullPath($ExpectedPath)
    if(-not [string]::Equals($processTask.Path,$expectedTask,[StringComparison]::OrdinalIgnoreCase) -or -not [string]::Equals($recordTask.path,$expectedTask,[StringComparison]::OrdinalIgnoreCase)) {
        $processTask.Dispose();throw 'Native process ownership mismatch. The recorded process is not this HollowCraft installation.'
    }
    return $processTask
}

function Show-HollowCraftNativeGameTask([int]$ProcessId) {
    if(-not ('HollowCraft.NativeLauncherWindow' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;
namespace HollowCraft {
    public static class NativeLauncherWindow {
        private delegate bool WindowCallback(IntPtr window, IntPtr state);
        [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, IntPtr state);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int length);
        [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr window, int command);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
        public static bool Restore(int process) {
            IntPtr selected = IntPtr.Zero;
            EnumWindows(delegate(IntPtr window, IntPtr state) {
                uint owner; GetWindowThreadProcessId(window, out owner);
                if (owner != (uint)process) return true;
                var name = new StringBuilder(256); GetClassName(window, name, name.Capacity);
                if (name.ToString().IndexOf("UnityWndClass", StringComparison.OrdinalIgnoreCase) < 0) return true;
                selected = window; return false;
            }, IntPtr.Zero);
            if (selected == IntPtr.Zero) return false;
            ShowWindowAsync(selected, 9); SetForegroundWindow(selected); return true;
        }
    }
}
'@
    }
    return [HollowCraft.NativeLauncherWindow]::Restore($ProcessId)
}
