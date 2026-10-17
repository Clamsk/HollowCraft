param(
    [Parameter(Mandatory=$true)][ValidateSet('Play','Install','Close')][string]$Action,
    [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$BossId,
    [ValidateRange(0,2)][int]$BossLevel=0,
    [ValidateRange(640,3840)][int]$Width=1280,
    [ValidateRange(480,2160)][int]$Height=720
)
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$OutputEncoding=[Console]::OutputEncoding
try {
    switch($Action) {
        'Play' {
            $argumentsTask=@{Width=$Width;Height=$Height}
            if($BossId){$argumentsTask.BossId=$BossId;$argumentsTask.BossLevel=$BossLevel}
            & (Join-Path $PSScriptRoot 'start-standalone.ps1') @argumentsTask
        }
        'Install' {& (Join-Path $PSScriptRoot 'install-standalone.ps1') -NonInteractive}
        'Close' {& (Join-Path $PSScriptRoot 'stop-standalone.ps1')}
    }
    exit 0
} catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
