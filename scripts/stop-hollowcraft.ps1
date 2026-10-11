$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
function OwnedNormalProcessTask([string]$Component) {
    $recordPath=Join-Path $taskRoot ('runtime/'+$Component+'-process.json')
    if(-not(Test-Path -LiteralPath $recordPath)) {return $null}
    $record=Get-Content -LiteralPath $recordPath -Raw|ConvertFrom-Json
    $process=Get-Process -Id $record.id -ErrorAction SilentlyContinue
    if(-not $process) {return $null}
    $expected=if($Component -eq 'minecraft') {'C:\Program Files\Microsoft\jdk-21.0.10.7-hotspot\bin\java.exe'}else{Join-Path $taskRoot 'runtime/Hollow Knight/hollow_knight.exe'}
    if($process.Path -ne $expected -or $process.StartTime.ToUniversalTime().Ticks -ne $record.startedTicks) {throw ('Process ownership mismatch: '+$Component)}
    return $process
}
$game=OwnedNormalProcessTask 'hollow-knight'
$java=OwnedNormalProcessTask 'minecraft'
if($game) {$null=$game.CloseMainWindow();if(-not $game.WaitForExit(5000)) {Stop-Process -Id $game.Id;$null=$game.WaitForExit(10000)}}
if($java) {
    $client=[Net.Sockets.TcpClient]::new()
    try {
        $client.Connect('127.0.0.1',31674)
        $client.ReceiveTimeout=10000
        $writer=[IO.StreamWriter]::new($client.GetStream(),[Text.UTF8Encoding]::new($false));$writer.AutoFlush=$true
        $writer.WriteLine('{"type":"hello","protocol":1}')
        $writer.WriteLine('{"type":"pause","paused":false}')
        $writer.WriteLine('{"type":"scene","scene":"Tutorial_01","x":34,"y":10}')
        $writer.WriteLine('{"type":"checkpoint","scene":"Tutorial_01"}')
        $reader=[IO.StreamReader]::new($client.GetStream());$saved=$false
        for($attempt=0;$attempt -lt 40;$attempt++) {$line=$reader.ReadLine();if($line -and $line.Contains('World and player data saved')) {$saved=$true;break}}
        if(-not $saved) {throw 'Minecraft save was not acknowledged; backend left running.'}
    } finally {$client.Dispose()}
    Stop-Process -Id $java.Id;$null=$java.WaitForExit(10000)
}
Write-Output 'HollowCraft stopped after saving Minecraft world and inventory.'
