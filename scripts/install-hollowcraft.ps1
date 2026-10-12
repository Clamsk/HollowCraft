param([string]$GameRoot,[string]$MinecraftRoot,[string]$JavaPath,[switch]$NonInteractive)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'hollowcraft-common.ps1')
Assert-HollowCraftStoppedTask
if(-not [Environment]::Is64BitOperatingSystem -or -not [Environment]::Is64BitProcess) {throw 'HollowCraft requires 64-bit Windows and 64-bit PowerShell.'}
$rootTask=$hollowCraftTaskRoot
$existingConfigTask=Join-Path $rootTask 'hollowcraft.local.json'
if(Test-Path -LiteralPath $existingConfigTask) {
    $previousTask=Get-HollowCraftConfigTask
    if(-not $GameRoot) {$GameRoot=$previousTask.gameRoot}
    if(-not $MinecraftRoot) {$MinecraftRoot=$previousTask.minecraftRoot}
    if(-not $JavaPath) {$JavaPath=$previousTask.javaPath}
}
function Select-InstallationPathTask([string]$Label,[string]$Current,[string[]]$Candidates,[string]$Required) {
    if($Current) {
        $currentTask=[IO.Path]::GetFullPath($Current.Trim('"'))
        if(Test-Path -LiteralPath (Join-Path $currentTask $Required)) {return $currentTask}
        throw ($Label+' not found: '+$currentTask)
    }
    foreach($candidateTask in $Candidates) {
        if($candidateTask -and (Test-Path -LiteralPath (Join-Path $candidateTask $Required))) {
            Write-Host ($Label+': '+$candidateTask)
            return [IO.Path]::GetFullPath($candidateTask)
        }
    }
    if($NonInteractive) {throw ('Specify '+$Label+' explicitly.')}
    $enteredTask=(Read-Host ('Enter '+$Label+' folder')).Trim('"')
    if(-not $enteredTask -or -not(Test-Path -LiteralPath (Join-Path $enteredTask $Required))) {throw ('Invalid '+$Label+' folder.')}
    return [IO.Path]::GetFullPath($enteredTask)
}
$steamCandidatesTask=@()
$steamRootTask=(Get-ItemProperty -LiteralPath 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).SteamPath
if($steamRootTask) {
    $steamCandidatesTask+=Join-Path $steamRootTask 'steamapps\common\Hollow Knight'
    $librariesTask=Join-Path $steamRootTask 'steamapps\libraryfolders.vdf'
    if(Test-Path -LiteralPath $librariesTask) {
        $vdfTask=Get-Content -LiteralPath $librariesTask -Raw -Encoding UTF8
        foreach($matchTask in [regex]::Matches($vdfTask,'"path"\s+"([^"]+)"')) {
            $steamCandidatesTask+=Join-Path ($matchTask.Groups[1].Value.Replace('\\','\')) 'steamapps\common\Hollow Knight'
        }
    }
}
$steamCandidatesTask+=Join-Path ${env:ProgramFiles(x86)} 'Steam\steamapps\common\Hollow Knight'
$GameRoot=Select-InstallationPathTask 'Steam Hollow Knight' $GameRoot $steamCandidatesTask 'hollow_knight_Data\Managed\Assembly-CSharp.dll'
$mcCandidatesTask=@((Join-Path $env:APPDATA '.minecraft'),(Join-Path ${env:ProgramFiles(x86)} 'MC\.minecraft'))
$MinecraftRoot=Select-InstallationPathTask 'Minecraft .minecraft (vanilla 1.20.1)' $MinecraftRoot $mcCandidatesTask 'versions\1.20.1\1.20.1.jar'
foreach($originalTask in @($GameRoot,$MinecraftRoot)) {
    if((Test-HollowCraftInsideTask $rootTask $originalTask) -or (Test-HollowCraftInsideTask $originalTask $rootTask)) {throw 'Extract HollowCraft into its own folder, outside both original game folders.'}
}
if($previousTask -and (Test-Path -LiteralPath (Join-Path $rootTask 'runtime\Hollow Knight'))) {
    if($previousTask.gameRoot -ne $GameRoot -or $previousTask.minecraftRoot -ne $MinecraftRoot) {throw 'For different game directories, extract into a fresh HollowCraft folder. Existing resource links must stay tied to their original installation.'}
}
if(-not $JavaPath) {
    $javaCandidatesTask=@()
    $javaCommandTask=Get-Command java.exe -ErrorAction SilentlyContinue
    if($javaCommandTask) {$javaCandidatesTask+=$javaCommandTask.Source}
    if($env:JAVA_HOME) {$javaCandidatesTask+=Join-Path $env:JAVA_HOME 'bin\java.exe'}
    foreach($baseTask in @('Microsoft','Java','Eclipse Adoptium')) {
        $javaBaseTask=Join-Path $env:ProgramFiles $baseTask
        if(Test-Path -LiteralPath $javaBaseTask) {
            $javaCandidatesTask+=Get-ChildItem -LiteralPath $javaBaseTask -Directory | Sort-Object Name -Descending | ForEach-Object {Join-Path $_.FullName 'bin\java.exe'}
        }
    }
    foreach($candidateTask in $javaCandidatesTask) {
        if(-not(Test-Path -LiteralPath $candidateTask)) {continue}
        $javaInfoTask=Get-HollowCraftJavaSettingsTask $candidateTask
        if($javaInfoTask -match 'java\.version\s*=\s*(17|21)\.' -and $javaInfoTask -match 'sun\.arch\.data\.model\s*=\s*64') {$JavaPath=$candidateTask;break}
    }
    if(-not $JavaPath -and -not $NonInteractive) {$JavaPath=(Read-Host 'Enter full path to 64-bit Java 17 or 21 java.exe').Trim('"')}
}
if(-not $JavaPath -or -not(Test-Path -LiteralPath $JavaPath)) {throw '64-bit Java 17 or 21 is required. See INSTALL_en.md or INSTALL_zh.md.'}
$JavaPath=(Resolve-Path -LiteralPath $JavaPath).Path
# Windows PowerShell treats stderr from native programs as errors; collect Java's
# normal version diagnostics without allowing them to abort the installation.
$javaInfoTask=Get-HollowCraftJavaSettingsTask $JavaPath
if($javaInfoTask -notmatch 'java\.version\s*=\s*(17|21)\.' -or $javaInfoTask -notmatch 'sun\.arch\.data\.model\s*=\s*64') {throw 'Use 64-bit Java 17 or 21.'}
foreach($requiredTask in @('versions\1.20.1\1.20.1.json','assets','libraries')) {
    if(-not(Test-Path -LiteralPath (Join-Path $MinecraftRoot $requiredTask))) {throw 'Launch vanilla Minecraft 1.20.1 once in your launcher before installing HollowCraft.'}
}
$manifestTask=Get-Content -LiteralPath (Join-Path $rootTask 'release\package-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach($fileTask in $manifestTask.files) {
    $targetTask=Join-Path $rootTask $fileTask.path
    if(-not(Test-HollowCraftInsideTask $targetTask $rootTask) -or (Get-FileHash -LiteralPath $targetTask -Algorithm SHA256).Hash -ne $fileTask.sha256) {throw ('Package integrity check failed: '+$fileTask.path)}
}
$lockTask=Get-Content -LiteralPath (Join-Path $rootTask 'release\dependencies.lock.json') -Raw -Encoding UTF8 | ConvertFrom-Json
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
function Get-VerifiedDependencyTask([string]$Url,[string]$Destination,[string]$Hash,[string]$Algorithm) {
    if($Url -notmatch '^https://') {throw 'Only HTTPS dependencies are supported.'}
    if((Test-Path -LiteralPath $Destination) -and (Get-FileHash -LiteralPath $Destination -Algorithm $Algorithm).Hash -eq $Hash) {return}
    New-Item -ItemType Directory -Path (Split-Path $Destination -Parent) -Force | Out-Null
    $partialTask=$Destination+'.download'
    Write-Host ('Downloading '+[IO.Path]::GetFileName($Destination))
    Invoke-WebRequest -UseBasicParsing -Uri $Url -OutFile $partialTask -Headers @{'User-Agent'='HollowCraft installer (https://github.com/Clamsk/HollowCraft)'}
    if((Get-FileHash -LiteralPath $partialTask -Algorithm $Algorithm).Hash -ne $Hash) {throw ('Dependency checksum mismatch: '+$Destination)}
    Move-Item -LiteralPath $partialTask -Destination $Destination -Force
}
foreach($modTask in $lockTask.mods) {Get-VerifiedDependencyTask $modTask.url (Join-Path $rootTask ('dependencies\mods\'+$modTask.filename)) $modTask.sha512 'SHA512'}
foreach($libTask in $lockTask.loaderLibraries) {Get-VerifiedDependencyTask $libTask.url (Join-Path $rootTask ('dependencies\fabric-libraries\'+$libTask.path.Replace('/','\'))) $libTask.sha1 'SHA1'}
$cecilPackageTask=Join-Path $rootTask 'dependencies\hk-api\mono.cecil.nupkg'
Get-VerifiedDependencyTask $lockTask.cecil.url $cecilPackageTask $lockTask.cecil.sha512 'SHA512'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipTask=[IO.Compression.ZipFile]::OpenRead($cecilPackageTask)
try {
    $entryTask=$zipTask.GetEntry('lib/net40/Mono.Cecil.dll')
    if(-not $entryTask) {throw 'Mono.Cecil package is missing its .NET Framework assembly.'}
    [IO.Compression.ZipFileExtensions]::ExtractToFile($entryTask,(Join-Path $rootTask 'dependencies\hk-api\Mono.Cecil.dll'),$true)
} finally {$zipTask.Dispose()}
@{schema=1;portable=$true;gameRoot=$GameRoot;minecraftRoot=$MinecraftRoot;javaPath=$JavaPath} | ConvertTo-Json | Set-Content -LiteralPath $existingConfigTask -Encoding UTF8
& (Join-Path $PSScriptRoot 'build-hollow-knight.ps1') -GameRoot $GameRoot -UsePrebuilt
& (Join-Path $PSScriptRoot 'prepare-hk-test.ps1') -GameRoot $GameRoot
& (Join-Path $PSScriptRoot 'prepare-mc-test.ps1') -MinecraftRoot $MinecraftRoot
Write-Host ('HollowCraft '+$manifestTask.version+' installed. Keep Steam signed in, then run Start HollowCraft.cmd.')
Write-Host 'This installer has prepared files only; it has not launched either game.'
