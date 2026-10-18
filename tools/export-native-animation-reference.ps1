param([string]$JavaHome='C:\Program Files\Microsoft\jdk-21.0.10.7-hotspot')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskOutput=Join-Path $taskRoot 'artifacts/native-alpha5-reference'
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
# Extract only the cached classpath; never output account/launcher arguments.
$taskArgs=[IO.File]::ReadAllLines((Join-Path $taskRoot 'runtime/minecraft/launch.args'))
$taskIndex=-1
for($taskI=0;$taskI -lt $taskArgs.Length;$taskI++){if($taskArgs[$taskI].Trim().Trim('"') -in '-cp','-classpath'){$taskIndex=$taskI;break}}
if($taskIndex -lt 0){throw 'Missing cached build classpath.'}
$taskMods='C:\Program Files (x86)\MC\.minecraft\versions\DarkRPG - RPG Quest Magic & Origins Online Adventure\mods'
$taskAnimator=Join-Path $taskMods 'player-animation-lib-fabric-1.0.2-rc1+1.20.jar'
$taskClasspath=$taskAnimator+';'+$taskArgs[$taskIndex+1].Trim('"')
$taskSource=Join-Path $PSScriptRoot 'NativeAnimationReference.java'
& (Join-Path $JavaHome 'bin/javac.exe') --release 17 -proc:none -cp $taskClasspath -d $taskOutput $taskSource
if($LASTEXITCODE -ne 0){throw 'Reference compilation failed.'}
& (Join-Path $JavaHome 'bin/java.exe') -cp ($taskOutput+';'+$taskClasspath) NativeAnimationReference (Join-Path $taskRoot 'artifacts/StandaloneAssets/bettercombat') (Join-Path $taskOutput 'samples.json')
if($LASTEXITCODE -ne 0){throw 'Reference export failed.'}
