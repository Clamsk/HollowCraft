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
    $saved=$false
    try {
        $client.Connect('127.0.0.1',31674)
        $client.ReceiveTimeout=10000
        $writer=[IO.StreamWriter]::new($client.GetStream(),[Text.UTF8Encoding]::new($false));$writer.AutoFlush=$true
        $writer.WriteLine('{"type":"hello","protocol":1}')
        $reader=[IO.StreamReader]::new($client.GetStream())
        $hello=$reader.ReadLine() | ConvertFrom-Json
        if($hello.type -ne 'hello' -or $hello.protocol -ne 1) {throw 'Minecraft bridge handshake mismatch; backend left running.'}
        if($hello.saveSession -eq $true) {
            $request=[Guid]::NewGuid().ToString('N')
            $writer.WriteLine('{"type":"pause","paused":false}')
            $writer.WriteLine((@{type='saveSession';request=$request} | ConvertTo-Json -Compress))
            for($attempt=0;$attempt -lt 64;$attempt++) {
                $line=$reader.ReadLine();if(-not $line) {break}
                $reply=$line | ConvertFrom-Json
                if($reply.type -eq 'sessionSaved' -and $reply.request -eq $request) {$saved=$true;break}
            }
            if(-not $saved) {throw 'Minecraft save was not acknowledged; backend left running.'}
        }
        $null=$java.CloseMainWindow()
        if(-not $java.WaitForExit(15000)) {
            if(-not $saved) {throw 'Older Minecraft backend has no save acknowledgement. Close its game window normally; it was not force-stopped.'}
            Stop-Process -Id $java.Id;$null=$java.WaitForExit(10000)
        }
    } finally {$client.Dispose()}
}
Write-Output 'HollowCraft stopped after confirmed saving or normal Minecraft window shutdown.'
