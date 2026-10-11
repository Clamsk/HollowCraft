param([int]$MinecraftId,[int]$HollowKnightId)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
function OwnedProcessTask([int]$ProcessId) {
    $recordFile=Join-Path $taskRoot ('runtime/test-process-'+$ProcessId+'.json')
    if(-not (Test-Path -LiteralPath $recordFile)) {throw ('Missing ownership record '+$ProcessId)}
    $record=Get-Content -LiteralPath $recordFile -Raw | ConvertFrom-Json
    $process=Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if($process -and ($process.StartTime.ToUniversalTime().Ticks -ne $record.startedTicks -or $process.Path -ne $record.path)) {throw ('Process ownership mismatch '+$ProcessId)}
    return $process
}
$game=OwnedProcessTask $HollowKnightId
$java=OwnedProcessTask $MinecraftId
if($game) {Stop-Process -Id $game.Id;$null=$game.WaitForExit(10000)}
if($java) {
    $client=[Net.Sockets.TcpClient]::new()
    try {
        $client.Connect('127.0.0.1',31674)
        $writer=[IO.StreamWriter]::new($client.GetStream(),[Text.UTF8Encoding]::new($false));$writer.AutoFlush=$true
        $writer.WriteLine('{"type":"hello","protocol":1}')
        $writer.WriteLine('{"type":"scene","scene":"Tutorial_01","x":34,"y":10}')
        $writer.WriteLine('{"type":"checkpoint","scene":"Tutorial_01"}')
        $client.ReceiveTimeout=10000
        $reader=[IO.StreamReader]::new($client.GetStream())
        $saved=$false
        for($attempt=0;$attempt -lt 30;$attempt++) {$line=$reader.ReadLine();if($line -and $line.Contains('World and player data saved')) {$saved=$true;break}}
        if(-not $saved) {throw 'Test checkpoint was not acknowledged'}
    } finally {$client.Dispose()}
    Stop-Process -Id $java.Id;$null=$java.WaitForExit(10000)
}
Write-Output 'Verified test processes stopped after acknowledged world checkpoint.'
