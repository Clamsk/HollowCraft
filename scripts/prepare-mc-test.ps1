param([string]$MinecraftRoot='C:\Program Files (x86)\MC\.minecraft',[switch]$Smoke)
$ErrorActionPreference='Stop'
$projectTaskRoot=Split-Path $PSScriptRoot -Parent
$profileTask=Join-Path $projectTaskRoot 'runtime\minecraft'
if($Smoke) {$profileTask=Join-Path $projectTaskRoot 'runtime\minecraft-smoke'}
$modsTask=Join-Path $profileTask 'mods'
New-Item -ItemType Directory -Path $modsTask -Force | Out-Null
Set-Content -LiteralPath (Join-Path $profileTask '.hollowcraft-profile') -Value 'HollowCraft isolated development profile' -Encoding utf8
Copy-Item -LiteralPath (Join-Path $projectTaskRoot 'minecraft\build\libs\hollowcraft-minecraft-0.1.0.jar') -Destination $modsTask -Force
$fabricTask=Get-ChildItem -LiteralPath (Join-Path $projectTaskRoot 'dependencies\gradle-home\caches\modules-2\files-2.1\net.fabricmc.fabric-api\fabric-api\0.92.6+1.20.1') -Recurse -Filter '*.jar' | Select-Object -First 1 -ExpandProperty FullName
if (-not $fabricTask) { throw 'Built Fabric API dependency not found' }
Copy-Item -LiteralPath $fabricTask -Destination $modsTask -Force
$combatModsTask=Join-Path $MinecraftRoot 'versions\DarkRPG - RPG Quest Magic & Origins Online Adventure\mods'
foreach($combatFileTask in @('bettercombat-fabric-1.8.5+1.20.1.jar','player-animation-lib-fabric-1.0.2-rc1+1.20.jar','cloth-config-11.1.118-fabric.jar')) {
    Copy-Item -LiteralPath (Join-Path $combatModsTask $combatFileTask) -Destination $modsTask -Force
}
$baseMetaTask=Get-Content -LiteralPath (Join-Path $MinecraftRoot 'versions\1.20.1\1.20.1.json') -Raw | ConvertFrom-Json
$fabricMetaTask=Get-Content -LiteralPath (Join-Path $MinecraftRoot 'versions\1.20.6-Fabric 0.18.4\1.20.6-Fabric 0.18.4.json') -Raw | ConvertFrom-Json
$pathsTask=[Collections.Generic.List[string]]::new()
foreach ($libTask in $baseMetaTask.libraries) {
    if ($libTask.rules) {
        $allowedTask=$true
        foreach($ruleTask in $libTask.rules) {
            if ($ruleTask.os -and $ruleTask.os.name -ne 'windows') { $allowedTask=$false }
            if (-not $ruleTask.os -and $ruleTask.action -eq 'disallow') { $allowedTask=$false }
        }
        if (-not $allowedTask) { continue }
    }
    if ($libTask.downloads.artifact.path) {
        $fileTask=Join-Path $MinecraftRoot ('libraries\'+$libTask.downloads.artifact.path.Replace('/','\'))
        if (Test-Path -LiteralPath $fileTask) { $pathsTask.Add($fileTask) }
        else { throw "Minecraft runtime dependency missing: $fileTask" }
    }
}
foreach ($libTask in $fabricMetaTask.libraries | Where-Object name -Match '^(net.fabricmc|org.ow2.asm):') {
    $partsTask=$libTask.name.Split(':')
    if ($partsTask[1] -eq 'intermediary') { $partsTask[2]='1.20.1' }
    $pathTask='libraries\'+$partsTask[0].Replace('.','\')+'\'+$partsTask[1]+'\'+$partsTask[2]+'\'+$partsTask[1]+'-'+$partsTask[2]+'.jar'
    $fileTask=Join-Path $MinecraftRoot $pathTask
    if (-not (Test-Path -LiteralPath $fileTask)) { throw "Fabric runtime dependency missing: $fileTask" }
    $pathsTask.Add($fileTask)
}
$pathsTask.Add((Join-Path $MinecraftRoot 'versions\1.20.1\1.20.1.jar'))
$classpathTask=$pathsTask -join ';'
$jvmTask=@('-Xmx2G','-Dhollowcraft.autoWorld=true',('-Dhollowcraft.icons='+(Join-Path $projectTaskRoot 'artifacts\HollowCraft\HollowCraftAssets\item_models_rgba')),('-Djava.library.path='+(Join-Path $MinecraftRoot 'versions\1.20.1\1.20.1-natives')),'-cp',$classpathTask)
if($Smoke) {$jvmTask=@('-Dhollowcraft.smoke=true')+$jvmTask}
$gameArgsTask=@('net.fabricmc.loader.impl.launch.knot.KnotClient','--username','HollowCraftDev','--version','HollowCraft-0.1','--gameDir',$profileTask,'--assetsDir',(Join-Path $MinecraftRoot 'assets'),'--assetIndex',$baseMetaTask.assetIndex.id,'--uuid','3d41453d09f04e99b4a738f943878f92','--accessToken','0','--userType','legacy','--width','960','--height','540')
# Java @argfile avoids Windows' command line length limit. This development launch has no account tokens.
$argumentLinesTask=($jvmTask+$gameArgsTask) | ForEach-Object { '"'+$_.Replace('\','\\').Replace('"','\"')+'"' }
# The Windows Java launcher decodes @argfiles with the native ANSI code page,
# even on Java 21 where normal Java file I/O defaults to UTF-8 (JEP 400).
# Use native.encoding reported by this exact runtime, not .NET's UTF-8 default.
$javaTask='C:\Program Files\Microsoft\jdk-21.0.10.7-hotspot\bin\java.exe'
$settingsTask=(& $javaTask -XshowSettings:properties -version 2>&1 | Out-String)
$encodingTaskName=if ($settingsTask -match 'native\.encoding\s*=\s*(\S+)') { $Matches[1] } else { throw 'Cannot determine Java native.encoding' }
[Text.Encoding]::RegisterProvider([Text.CodePagesEncodingProvider]::Instance)
$encodingTask=[Text.Encoding]::GetEncoding($encodingTaskName,[Text.EncoderFallback]::ExceptionFallback,[Text.DecoderFallback]::ExceptionFallback)
$argumentTextTask=($argumentLinesTask -join "`n")+"`n"
$argumentBytesTask=$encodingTask.GetBytes($argumentTextTask)
if ($encodingTask.GetString($argumentBytesTask) -cne $argumentTextTask) { throw 'Java launch arguments do not round-trip through native.encoding' }
[IO.File]::WriteAllBytes((Join-Path $profileTask 'launch.args'),$argumentBytesTask)
Write-Output "Java argument file encoding: $encodingTaskName (round-trip verified)"
Write-Output "Isolated Minecraft profile prepared: $profileTask"
