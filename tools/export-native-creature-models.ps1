param([string]$JavaHome='C:\Program Files\Microsoft\jdk-21.0.10.7-hotspot')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskOutput=Join-Path $taskRoot 'artifacts/native-creature-export'
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskArgs=[IO.File]::ReadAllLines((Join-Path $taskRoot 'runtime/minecraft/launch.args'))
$taskIndex=-1;for($taskI=0;$taskI -lt $taskArgs.Length;$taskI++){if($taskArgs[$taskI].Trim().Trim('"') -in '-cp','-classpath'){$taskIndex=$taskI;break}}
if($taskIndex -lt 0){throw 'Missing cached build classpath.'}
$taskMapped=Join-Path $taskRoot 'artifacts/native-registry-export/minecraft-build-access.jar'
if(-not(Test-Path -LiteralPath $taskMapped)){throw 'Run tools/export-native-rules.ps1 to prepare the local mapped build dependency.'}
$taskClasspath=$taskMapped+';'+$taskArgs[$taskIndex+1].Trim('"')
& (Join-Path $JavaHome 'bin/javac.exe') --release 17 -proc:none -cp $taskClasspath -d $taskOutput (Join-Path $PSScriptRoot 'NativeCreatureModelExporter.java')
if($LASTEXITCODE -ne 0){throw 'Model exporter compilation failed.'}
Push-Location $taskOutput
try {
& (Join-Path $JavaHome 'bin/java.exe') -cp ($taskOutput+';'+$taskClasspath) NativeCreatureModelExporter (Join-Path $taskRoot 'artifacts/native-creature-export/models.json')
if($LASTEXITCODE -ne 0){throw 'Model export failed.'}

}finally{Pop-Location}
