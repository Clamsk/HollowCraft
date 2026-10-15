param([string]$MinecraftRoot,[switch]$Smoke)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'hollowcraft-common.ps1')
$configTask=Get-HollowCraftConfigTask
if(-not $MinecraftRoot) {$MinecraftRoot=$configTask.minecraftRoot}
$projectTaskRoot=Split-Path $PSScriptRoot -Parent
$profileTask=Join-Path $projectTaskRoot 'runtime\minecraft'
if($Smoke) {$profileTask=Join-Path $projectTaskRoot 'runtime\minecraft-smoke'}
$modsTask=Join-Path $profileTask 'mods'
New-Item -ItemType Directory -Path $modsTask -Force | Out-Null
Set-Content -LiteralPath (Join-Path $profileTask '.hollowcraft-profile') -Value 'HollowCraft isolated development profile' -Encoding utf8
Copy-Item -LiteralPath (Join-Path $projectTaskRoot 'minecraft\build\libs\hollowcraft-minecraft-0.1.0.jar') -Destination $modsTask -Force
if($configTask.portable) {
    $lockTask=Get-Content -LiteralPath (Join-Path $projectTaskRoot 'release\dependencies.lock.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach($modTask in $lockTask.mods) {
        Copy-Item -LiteralPath (Join-Path $projectTaskRoot ('dependencies\mods\'+$modTask.filename)) -Destination $modsTask -Force
    }
} else {
    $fabricTask=Get-ChildItem -LiteralPath (Join-Path $projectTaskRoot 'dependencies\gradle-home\caches\modules-2\files-2.1\net.fabricmc.fabric-api\fabric-api\0.92.6+1.20.1') -Recurse -Filter '*.jar' | Select-Object -First 1 -ExpandProperty FullName
    if (-not $fabricTask) { throw 'Built Fabric API dependency not found' }
    Copy-Item -LiteralPath $fabricTask -Destination $modsTask -Force
    $combatModsTask=Join-Path $MinecraftRoot 'versions\DarkRPG - RPG Quest Magic & Origins Online Adventure\mods'
    foreach($combatFileTask in @('bettercombat-fabric-1.8.5+1.20.1.jar','player-animation-lib-fabric-1.0.2-rc1+1.20.jar','cloth-config-11.1.118-fabric.jar')) {
        Copy-Item -LiteralPath (Join-Path $combatModsTask $combatFileTask) -Destination $modsTask -Force
    }
}
$baseMetaTask=Get-Content -LiteralPath (Join-Path $MinecraftRoot 'versions\1.20.1\1.20.1.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if(-not $configTask.portable) {$fabricMetaTask=Get-Content -LiteralPath (Join-Path $MinecraftRoot 'versions\1.20.6-Fabric 0.18.4\1.20.6-Fabric 0.18.4.json') -Raw -Encoding UTF8 | ConvertFrom-Json}
$pathsTask=[Collections.Generic.List[string]]::new()
foreach ($libTask in $baseMetaTask.libraries) {
    if($libTask.name -match ':natives-windows-(arm64|x86)$') {continue}
    if ($libTask.rules) {
        $allowedTask=$false
        foreach($ruleTask in $libTask.rules) {
            $matchesTask=$true
            if($ruleTask.os.name -and $ruleTask.os.name -ne 'windows') {$matchesTask=$false}
            if($ruleTask.os.arch -and $ruleTask.os.arch -ne 'x86_64') {$matchesTask=$false}
            if($ruleTask.os.version -and [Environment]::OSVersion.Version.ToString() -notmatch $ruleTask.os.version) {$matchesTask=$false}
            if($ruleTask.features) {$matchesTask=$false}
            if($matchesTask) {$allowedTask=$ruleTask.action -eq 'allow'}
        }
        if (-not $allowedTask) { continue }
    }
    if ($libTask.downloads.artifact.path) {
        $fileTask=Join-Path $MinecraftRoot ('libraries\'+$libTask.downloads.artifact.path.Replace('/','\'))
        if (Test-Path -LiteralPath $fileTask) { $pathsTask.Add($fileTask) }
        else { throw "Minecraft runtime dependency missing: $fileTask" }
    }
}
if($configTask.portable) {
    foreach($libTask in $lockTask.loaderLibraries) {
        $fileTask=Join-Path $projectTaskRoot ('dependencies\fabric-libraries\'+$libTask.path.Replace('/','\'))
        if(-not(Test-Path -LiteralPath $fileTask)) {throw 'Fabric library missing. Run Install HollowCraft.cmd.'}
        $pathsTask.Add($fileTask)
    }
} else { foreach ($libTask in $fabricMetaTask.libraries | Where-Object name -Match '^(net.fabricmc|org.ow2.asm):') {
    $partsTask=$libTask.name.Split(':')
    if ($partsTask[1] -eq 'intermediary') { $partsTask[2]='1.20.1' }
    $pathTask='libraries\'+$partsTask[0].Replace('.','\')+'\'+$partsTask[1]+'\'+$partsTask[2]+'\'+$partsTask[1]+'-'+$partsTask[2]+'.jar'
    $fileTask=Join-Path $MinecraftRoot $pathTask
    if (-not (Test-Path -LiteralPath $fileTask)) { throw "Fabric runtime dependency missing: $fileTask" }
    $pathsTask.Add($fileTask)
} }
$pathsTask.Add((Join-Path $MinecraftRoot 'versions\1.20.1\1.20.1.jar'))
$classpathTask=$pathsTask -join ';'
$nativesTask=$configTask.nativesPath
if($configTask.portable) {
    $nativesTask=Join-Path $profileTask 'natives'
    New-Item -ItemType Directory -Path $nativesTask -Force | Out-Null
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    foreach($nativeJarTask in $pathsTask | Where-Object {$_ -match '-natives-windows\.jar$'}) {
        $zipTask=[IO.Compression.ZipFile]::OpenRead($nativeJarTask)
        try {
            foreach($entryTask in $zipTask.Entries | Where-Object {$_.Name -match '\.dll$'}) {
                [IO.Compression.ZipFileExtensions]::ExtractToFile($entryTask,(Join-Path $nativesTask $entryTask.Name),$true)
            }
        } finally {$zipTask.Dispose()}
    }
    if(-not(Test-Path -LiteralPath (Join-Path $nativesTask 'lwjgl.dll'))) {throw 'Minecraft Windows natives missing. Launch vanilla 1.20.1 once with your launcher, then reinstall.'}
}
$jvmTask=@('-Xmx2G','-Dhollowcraft.autoWorld=true',('-Dhollowcraft.icons='+(Join-Path $projectTaskRoot 'artifacts\HollowCraft\HollowCraftAssets\item_models_rgba')),('-Djava.library.path='+$nativesTask),'-cp',$classpathTask)
if($Smoke) {$jvmTask=@('-Dhollowcraft.smoke=true')+$jvmTask}
$jvmTask=@('-Dhollowcraft.audio='+(Join-Path $projectTaskRoot 'artifacts\HollowCraft\HollowCraftAssets\mc_audio'))+$jvmTask
$gameArgsTask=@('net.fabricmc.loader.impl.launch.knot.KnotClient','--username','HollowCraftDev','--version','HollowCraft-0.1','--gameDir',$profileTask,'--assetsDir',(Join-Path $MinecraftRoot 'assets'),'--assetIndex',$baseMetaTask.assetIndex.id,'--uuid','3d41453d09f04e99b4a738f943878f92','--accessToken','0','--userType','legacy','--width','960','--height','540')
# Java @argfile avoids Windows' command line length limit. This development launch has no account tokens.
$argumentLinesTask=($jvmTask+$gameArgsTask) | ForEach-Object { '"'+$_.Replace('\','\\').Replace('"','\"')+'"' }
# The Windows Java launcher decodes @argfiles with the native ANSI code page,
# even on Java 21 where normal Java file I/O defaults to UTF-8 (JEP 400).
# Use native.encoding reported by this exact runtime, not .NET's UTF-8 default.
$javaTask=$configTask.javaPath
$settingsTask=Get-HollowCraftJavaSettingsTask $javaTask
$encodingTaskName=if ($settingsTask -match 'native\.encoding\s*=\s*(\S+)') { $Matches[1] } elseif($settingsTask -match 'file\.encoding\s*=\s*(\S+)') {$Matches[1]} else { throw 'Cannot determine Java launch encoding' }
if($PSVersionTable.PSEdition -eq 'Core') {[Text.Encoding]::RegisterProvider([Text.CodePagesEncodingProvider]::Instance)}
$encodingTask=[Text.Encoding]::GetEncoding($encodingTaskName,[Text.EncoderFallback]::ExceptionFallback,[Text.DecoderFallback]::ExceptionFallback)
$argumentTextTask=($argumentLinesTask -join "`n")+"`n"
$argumentBytesTask=$encodingTask.GetBytes($argumentTextTask)
if ($encodingTask.GetString($argumentBytesTask) -cne $argumentTextTask) { throw 'Java launch arguments do not round-trip through native.encoding' }
[IO.File]::WriteAllBytes((Join-Path $profileTask 'launch.args'),$argumentBytesTask)
Write-Output "Java argument file encoding: $encodingTaskName (round-trip verified)"
Write-Output "Isolated Minecraft profile prepared: $profileTask"
if(-not $Smoke) {& (Join-Path $PSScriptRoot 'prepare-terraria-assets.ps1')}
