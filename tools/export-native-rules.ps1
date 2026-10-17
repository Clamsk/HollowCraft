param([string]$JavaHome='C:\Program Files\Microsoft\jdk-21.0.10.7-hotspot')
$ErrorActionPreference='Stop'
$nativeRootTask=Split-Path $PSScriptRoot -Parent
$outputTask=Join-Path $nativeRootTask 'artifacts\native-registry-export'
New-Item -ItemType Directory -Path $outputTask -Force | Out-Null
# Only read the existing classpath argument. Never log launcher/account arguments.
$launchTask=[IO.File]::ReadAllLines((Join-Path $nativeRootTask 'runtime\minecraft\launch.args'))
$indexTask=-1;for($iTask=0;$iTask -lt $launchTask.Length;$iTask++){if($launchTask[$iTask].Trim().Trim('"') -in '-cp','-classpath'){$indexTask=$iTask;break}}
if($indexTask -lt 0){throw 'No cached build classpath found.'}
$classpathTask=$launchTask[$indexTask+1].Trim('"')
$mappedTask=Get-ChildItem -LiteralPath (Join-Path $nativeRootTask 'dependencies\gradle-home\caches\fabric-loom\minecraftMaven\net\minecraft\minecraft-merged') -Filter '*.jar' -Recurse | Select-Object -First 1 -ExpandProperty FullName
if(-not $mappedTask){throw 'Local mapped build dependency missing.'}
$classpathTask=$mappedTask+';'+$classpathTask
$sourceTask=Join-Path $PSScriptRoot 'NativeRegistryExporter.java'
$fixerTask=Join-Path $PSScriptRoot 'NativeAccessFixer.java'
$argumentsTask=@('--release','17','-proc:none','-cp',('"'+$classpathTask.Replace('\','/')+'"'),'-d',('"'+$outputTask.Replace('\','/')+'"'),('"'+$sourceTask.Replace('\','/')+'"'),('"'+$fixerTask.Replace('\','/')+'"'))
$argFileTask=Join-Path $outputTask 'compile.args';[IO.File]::WriteAllLines($argFileTask,$argumentsTask,[Text.UTF8Encoding]::new($false))
& (Join-Path $JavaHome 'bin\javac.exe') ('@'+$argFileTask)
if($LASTEXITCODE -ne 0){throw 'Native registry build tool compilation failed.'}
$publicTask=Join-Path $outputTask 'minecraft-build-access.jar'
& (Join-Path $JavaHome 'bin\java.exe') '-cp' ($outputTask+';'+$classpathTask) 'NativeAccessFixer' $mappedTask $publicTask
if($LASTEXITCODE -ne 0){throw 'Local mapped build dependency access repair failed.'}
$classpathTask=$publicTask+';'+$classpathTask
Push-Location $outputTask
try{& (Join-Path $JavaHome 'bin\java.exe') '-cp' ($outputTask+';'+$classpathTask) 'NativeRegistryExporter' (Join-Path $nativeRootTask 'release\native-registry.json');if($LASTEXITCODE -ne 0){throw 'Native registry export failed.'}}finally{Pop-Location}
