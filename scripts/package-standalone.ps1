param([string]$Version='1.0.0')
$ErrorActionPreference='Stop'
$nativeRootTask=Split-Path $PSScriptRoot -Parent
$dllTask=Join-Path $nativeRootTask 'artifacts\HollowCraft\HollowCraft.dll'
if($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(-[A-Za-z0-9.-]+)?$'){throw 'Use a semantic version such as 1.0.0 or 1.1.0-alpha.1.'}
if([Reflection.AssemblyName]::GetAssemblyName($dllTask).Version.ToString(3) -ne ($Version -split '-')[0]){throw 'Native DLL assembly version mismatch.'}
if((Get-Item -LiteralPath $dllTask).VersionInfo.ProductVersion.Split('+')[0] -ne $Version){throw 'Native DLL release version mismatch.'}
$stageTask=Join-Path $nativeRootTask ('artifacts\native-release-staging\'+[Guid]::NewGuid().ToString('N')+'\HollowCraft-Native')
New-Item -ItemType Directory -Path $stageTask -Force | Out-Null
$filesTask=@(
    'Install HollowCraft.cmd','Start HollowCraft.cmd','Stop HollowCraft.cmd','HollowCraft Launcher.cmd','Boss Recording.cmd',
    'Install HollowCraft Native.cmd','Start HollowCraft Native.cmd','Stop HollowCraft Native.cmd',
    'scripts\hollowcraft-common.ps1','scripts\build-hollow-knight.ps1','scripts\prepare-hk-test.ps1',
    'scripts\install-standalone.ps1','scripts\start-standalone.ps1','scripts\stop-standalone.ps1',
    'scripts\launcher-native.ps1','scripts\native-launcher-run.ps1','scripts\native-launcher-process.ps1','scripts\native-launcher-settings.ps1',
    'release\launcher\launcher.xaml','release\launcher\cavern-voxel-v1.png','release\launcher\NativeLauncherIcons.cs','release\native-held-models.json',
    'scripts\import-native-assets.ps1','scripts\prepare-native-resources.ps1','scripts\prepare-native-audio.ps1','scripts\prepare-native-font.ps1','scripts\prepare-terraria-assets.ps1',
    'tools\TerrariaAssetImporter\Importer.cs','tools\TerrariaAssetImporter\LzxDecoder.cs','tools\TerrariaAssetImporter\LzxDecoderStream.cs','tools\TerrariaAssetImporter\LICENSE.txt','tools\TerrariaAssetImporter\SOURCE.md',
    'release\native-dependencies.lock.json','release\native-migration.json','release\native-registry.json','release\native-creature-models.json','release\native-trades.json','release\bosses.json',
    'docs\NATIVE_MIGRATION.md','docs\NATIVE_MIGRATION.en.md','docs\NATIVE_LAUNCHER_FIX.md','docs\NATIVE_HANDS_LAUNCHER.md','docs\NATIVE_HANDS_LAUNCHER.en.md','docs\LAUNCHER_ARTWORK.md',
    'docs\NATIVE_RENDERING.md','docs\NATIVE_RENDERING.en.md','docs\NATIVE_INPUT_FIX.md','docs\NATIVE_INPUT_FIX.en.md','docs\NATIVE_ANIMATION_FIX.md','docs\NATIVE_ANIMATION_FIX.en.md',
    'docs\NATIVE_SYSTEMS.md','docs\NATIVE_SYSTEMS.en.md','docs\NATIVE_EXPANSION.md','docs\NATIVE_EXPANSION.en.md','docs\NATIVE_FLUID_FOOD.md','docs\NATIVE_FLUID_FOOD.en.md','docs\NATIVE_TERRAIN_TRIDENT.md','docs\NATIVE_TERRAIN_TRIDENT.en.md','tools\fixtures\tutorial-edge-floor.json',
    'docs\NATIVE_ZENITH_BALANCE.md','docs\NATIVE_ZENITH_BALANCE.en.md',
    'docs\NATIVE_LAVA_POTION.md','docs\NATIVE_LAVA_POTION.en.md',
    'docs\NATIVE_RELEASE_1.0.md','docs\NATIVE_RELEASE_1.0.en.md',
    'release\THIRD_PARTY.md','release\THIRD_PARTY.en.md'
)
foreach($relativeTask in $filesTask){
    $destinationTask=Join-Path $stageTask $relativeTask
    New-Item -ItemType Directory -Path (Split-Path $destinationTask -Parent) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $nativeRootTask $relativeTask) -Destination $destinationTask
}
New-Item -ItemType Directory -Path (Join-Path $stageTask 'artifacts\HollowCraft') -Force | Out-Null
Copy-Item -LiteralPath $dllTask -Destination (Join-Path $stageTask 'artifacts\HollowCraft\HollowCraft.dll')
Set-Content -LiteralPath (Join-Path $stageTask 'README.md') -Encoding UTF8 -Value ('# HollowCraft Native '+$Version), '', '只运行空洞的独立 Mod。首个正式发布版，功能基于 0.40.0-alpha.12。', '', '先运行 Install HollowCraft Native.cmd，再运行 HollowCraft Launcher.cmd。更新时保留 runtime、hollowcraft.native.local.json 和 hollowcraft.native.settings.json。', '', '[正式版说明与已知限制](docs/NATIVE_RELEASE_1.0.md) | [完整安装说明](docs/NATIVE_MIGRATION.md) | [English](docs/NATIVE_MIGRATION.en.md)'
Set-Content -LiteralPath (Join-Path $stageTask 'README.en.md') -Encoding UTF8 -Value ('# HollowCraft Native '+$Version), '', 'Hollow Knight-only mod. First regular release, based on 0.40.0-alpha.12.', '', 'Run Install HollowCraft Native.cmd, then HollowCraft Launcher.cmd. Preserve runtime, hollowcraft.native.local.json and hollowcraft.native.settings.json when updating.', '', '[Release notes and known limitations](docs/NATIVE_RELEASE_1.0.en.md) | [Installation](docs/NATIVE_MIGRATION.en.md) | [简体中文](docs/NATIVE_MIGRATION.md)'
$manifestFilesTask=@(Get-ChildItem -LiteralPath $stageTask -Recurse -File | ForEach-Object {
    @{path=$_.FullName.Substring($stageTask.Length+1).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
})
@{schema=1;version=$Version;backend='standalone';gameVersion='1.5.12620';files=$manifestFilesTask} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $stageTask 'release\native-package-manifest.json') -Encoding UTF8
# Explicit file list above excludes original game files, extracted assets, dependencies,
# all Java/JAR binaries, frame caches, accounts, logs and player saves.
Add-Type -AssemblyName System.IO.Compression,System.IO.Compression.FileSystem
$zipTask=Join-Path $nativeRootTask ('artifacts\HollowCraft-Native-Windows-'+$Version+'.zip')
if(Test-Path -LiteralPath $zipTask){throw 'Package exists. Choose a new version or archive it before packaging.'}
$archiveTask=[IO.Compression.ZipFile]::Open($zipTask,[IO.Compression.ZipArchiveMode]::Create)
try{foreach($fileTask in Get-ChildItem -LiteralPath $stageTask -Recurse -File){$entryNameTask='HollowCraft-Native/'+$fileTask.FullName.Substring($stageTask.Length+1).Replace('\','/');$null=[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archiveTask,$fileTask.FullName,$entryNameTask,[IO.Compression.CompressionLevel]::Optimal)}}finally{$archiveTask.Dispose()}
$hashTask=(Get-FileHash -LiteralPath $zipTask -Algorithm SHA256).Hash
[IO.File]::WriteAllText($zipTask+'.sha256',$hashTask.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($zipTask)+[Environment]::NewLine,[Text.UTF8Encoding]::new($false))
Write-Output $zipTask
Write-Output ('SHA256 '+$hashTask)
