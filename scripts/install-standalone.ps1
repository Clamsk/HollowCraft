param([string]$GameRoot,[switch]$Build,[switch]$NonInteractive,[switch]$SkipVisualResources)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'hollowcraft-common.ps1')
$nativeRootTask=Split-Path $PSScriptRoot -Parent
$nativeConfigTask=Join-Path $nativeRootTask 'hollowcraft.native.local.json'
$nativeRecordTask=Join-Path $nativeRootTask 'runtime\hollow-knight-native-process.json'
if(Test-Path -LiteralPath $nativeRecordTask) {
    $recordTask=Get-Content -LiteralPath $nativeRecordTask -Raw | ConvertFrom-Json
    $processTask=Get-Process -Id $recordTask.id -ErrorAction SilentlyContinue
    if($processTask -and $processTask.StartTime.ToUniversalTime().Ticks -eq $recordTask.startedTicks){throw 'Close the native HollowCraft game before installing.'}
}
if(Test-Path -LiteralPath $nativeConfigTask) {
    $priorTask=Get-Content -LiteralPath $nativeConfigTask -Raw -Encoding UTF8 | ConvertFrom-Json
    if(-not $GameRoot){$GameRoot=$priorTask.gameRoot}
    if($GameRoot -and $priorTask.gameRoot -ne [IO.Path]::GetFullPath($GameRoot)){throw 'Use a fresh HollowCraft folder for a different game installation.'}
}
if(-not $GameRoot) {
    $steamTask=(Get-ItemProperty -LiteralPath 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).SteamPath
    $candidatesTask=@()
    if($steamTask) {
        $candidatesTask+=Join-Path $steamTask 'steamapps\common\Hollow Knight'
        $librariesTask=Join-Path $steamTask 'steamapps\libraryfolders.vdf'
        if(Test-Path -LiteralPath $librariesTask) {
            foreach($matchTask in [regex]::Matches((Get-Content -LiteralPath $librariesTask -Raw),'"path"\s+"([^"]+)"')) {
                $candidatesTask+=Join-Path ($matchTask.Groups[1].Value.Replace('\\','\')) 'steamapps\common\Hollow Knight'
            }
        }
    }
    $candidatesTask+=Join-Path ${env:ProgramFiles(x86)} 'Steam\steamapps\common\Hollow Knight'
    $GameRoot=$candidatesTask | Where-Object {Test-Path -LiteralPath (Join-Path $_ 'hollow_knight_Data\Managed\Assembly-CSharp.dll')} | Select-Object -First 1
    if(-not $GameRoot -and -not $NonInteractive){$GameRoot=(Read-Host 'Steam Hollow Knight folder').Trim('"')}
}
if(-not $GameRoot -or -not(Test-Path -LiteralPath (Join-Path $GameRoot 'hollow_knight.exe'))){throw 'Specify the Steam Hollow Knight installation with -GameRoot.'}
$GameRoot=[IO.Path]::GetFullPath($GameRoot)
if((Test-HollowCraftInsideTask $nativeRootTask $GameRoot) -or (Test-HollowCraftInsideTask $GameRoot $nativeRootTask)){throw 'Extract HollowCraft outside the original game directory.'}
$manifestTask=Join-Path $nativeRootTask 'release\native-package-manifest.json'
if(Test-Path -LiteralPath $manifestTask) {
    foreach($entryTask in (Get-Content -LiteralPath $manifestTask -Raw | ConvertFrom-Json).files) {
        $fileTask=Join-Path $nativeRootTask $entryTask.path
        if(-not(Test-HollowCraftInsideTask $fileTask $nativeRootTask) -or -not(Test-Path -LiteralPath $fileTask) -or (Get-FileHash -LiteralPath $fileTask -Algorithm SHA256).Hash -ne $entryTask.sha256){throw ('Native package integrity failed: '+$entryTask.path)}
    }
}
$cecilTask=Join-Path $nativeRootTask 'dependencies\hk-api\Mono.Cecil.dll'
if(-not(Test-Path -LiteralPath $cecilTask)) {
    $lockTask=Get-Content -LiteralPath (Join-Path $nativeRootTask 'release\native-dependencies.lock.json') -Raw | ConvertFrom-Json
    $downloadTask=Join-Path $nativeRootTask 'dependencies\mono.cecil.nupkg'
    New-Item -ItemType Directory -Path (Split-Path $cecilTask -Parent) -Force | Out-Null
    [Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -UseBasicParsing -Uri $lockTask.cecil.url -OutFile $downloadTask
    if((Get-FileHash -LiteralPath $downloadTask -Algorithm SHA512).Hash -ne $lockTask.cecil.sha512){throw 'Mono.Cecil package checksum mismatch.'}
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zipTask=[IO.Compression.ZipFile]::OpenRead($downloadTask)
    try {[IO.Compression.ZipFileExtensions]::ExtractToFile($zipTask.GetEntry('lib/net40/Mono.Cecil.dll'),$cecilTask,$true)} finally {$zipTask.Dispose()}
}
& (Join-Path $PSScriptRoot 'build-hollow-knight.ps1') -GameRoot $GameRoot -UsePrebuilt:(-not $Build) -SkipAssets
$nativeAssetsTask=Join-Path $nativeRootTask 'artifacts/StandaloneAssets';New-Item -ItemType Directory -Path $nativeAssetsTask -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $nativeRootTask 'release/native-registry.json') -Destination (Join-Path $nativeAssetsTask 'native-registry.json') -Force
Copy-Item -LiteralPath (Join-Path $nativeRootTask 'release/native-held-models.json') -Destination (Join-Path $nativeAssetsTask 'native-held-models.json') -Force
Copy-Item -LiteralPath (Join-Path $nativeRootTask 'release/native-trades.json') -Destination (Join-Path $nativeAssetsTask 'native-trades.json') -Force
Copy-Item -LiteralPath (Join-Path $nativeRootTask 'release/native-creature-models.json') -Destination (Join-Path $nativeAssetsTask 'native-creature-models.json') -Force
if(-not $SkipVisualResources){try{& (Join-Path $PSScriptRoot 'prepare-native-resources.ps1')}catch{Write-Warning ('Static resource setup failed; using authored fallback assets. Retry install when online. '+$_.Exception.Message)}}
if(-not $SkipVisualResources -and -not(Test-Path -LiteralPath (Join-Path $nativeAssetsTask 'terraria/ready.txt'))){try{& (Join-Path $PSScriptRoot 'prepare-terraria-assets.ps1') -AssetsRoot $nativeAssetsTask -NativeOnly}catch{Write-Warning ('Optional local Terraria artwork import skipped: '+$_.Exception.Message)}}
& (Join-Path $PSScriptRoot 'prepare-hk-test.ps1') -GameRoot $GameRoot -Standalone
@{schema=1;backend='standalone';gameRoot=$GameRoot;version='0.40.0-alpha.9'} | ConvertTo-Json | Set-Content -LiteralPath $nativeConfigTask -Encoding UTF8
Write-Host 'Native HollowCraft installed. Open Start HollowCraft Native.cmd. Minecraft and Java are not required.'
