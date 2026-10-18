param([string]$Version='0.40.0-alpha.5')
$ErrorActionPreference='Stop'
$nativeRootTask=Split-Path $PSScriptRoot -Parent
$dllTask=Join-Path $nativeRootTask 'artifacts\HollowCraft\HollowCraft.dll'
if($Version -notmatch '^0\.40\.0-alpha\.[0-9]+$'){throw 'This packager is for native 0.40.0 alpha releases.'}
if([Reflection.AssemblyName]::GetAssemblyName($dllTask).Version.ToString(3) -ne '0.40.0'){throw 'Native DLL version mismatch.'}
$stageTask=Join-Path $nativeRootTask ('artifacts\native-release-staging\'+[Guid]::NewGuid().ToString('N')+'\HollowCraft-Native')
New-Item -ItemType Directory -Path $stageTask -Force | Out-Null
$filesTask=@(
    'Install HollowCraft.cmd','Start HollowCraft.cmd','Stop HollowCraft.cmd','HollowCraft Launcher.cmd','Boss Recording.cmd',
    'Install HollowCraft Native.cmd','Start HollowCraft Native.cmd','Stop HollowCraft Native.cmd',
    'scripts\hollowcraft-common.ps1','scripts\build-hollow-knight.ps1','scripts\prepare-hk-test.ps1',
    'scripts\install-standalone.ps1','scripts\start-standalone.ps1','scripts\stop-standalone.ps1',
    'scripts\launcher-native.ps1','scripts\native-launcher-run.ps1','scripts\native-launcher-process.ps1',
    'scripts\import-native-assets.ps1','scripts\prepare-native-resources.ps1','scripts\prepare-native-audio.ps1','scripts\prepare-native-font.ps1',
    'release\native-dependencies.lock.json','release\native-migration.json','release\native-registry.json','release\bosses.json',
    'docs\NATIVE_MIGRATION.md','docs\NATIVE_MIGRATION.en.md','docs\NATIVE_LAUNCHER_FIX.md',
    'docs\NATIVE_RENDERING.md','docs\NATIVE_RENDERING.en.md','docs\NATIVE_INPUT_FIX.md','docs\NATIVE_INPUT_FIX.en.md','docs\NATIVE_ANIMATION_FIX.md','docs\NATIVE_ANIMATION_FIX.en.md',
    'release\THIRD_PARTY.md','release\THIRD_PARTY.en.md'
)
foreach($relativeTask in $filesTask){
    $destinationTask=Join-Path $stageTask $relativeTask
    New-Item -ItemType Directory -Path (Split-Path $destinationTask -Parent) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $nativeRootTask $relativeTask) -Destination $destinationTask
}
New-Item -ItemType Directory -Path (Join-Path $stageTask 'artifacts\HollowCraft') -Force | Out-Null
Copy-Item -LiteralPath $dllTask -Destination (Join-Path $stageTask 'artifacts\HollowCraft\HollowCraft.dll')
Set-Content -LiteralPath (Join-Path $stageTask 'README.md') -Encoding UTF8 -Value '# HollowCraft Native 0.40.0-alpha.5', '', '只运行空洞的独立 Mod 迁移预览。尚未完成全部功能移植，尚未验证完整通关。', '', '先运行 Install HollowCraft Native.cmd，再运行 Start HollowCraft Native.cmd。', '', '[完整安装与迁移说明](docs/NATIVE_MIGRATION.md) | [English](docs/NATIVE_MIGRATION.en.md)'
Set-Content -LiteralPath (Join-Path $stageTask 'README.en.md') -Encoding UTF8 -Value '# HollowCraft Native 0.40.0-alpha.5', '', 'Hollow Knight-only migration preview. Feature parity and a full playthrough are not yet verified.', '', 'Run Install HollowCraft Native.cmd, then Start HollowCraft Native.cmd.', '', '[Installation and migration status](docs/NATIVE_MIGRATION.en.md) | [简体中文](docs/NATIVE_MIGRATION.md)'
$manifestFilesTask=@(Get-ChildItem -LiteralPath $stageTask -Recurse -File | ForEach-Object {
    @{path=$_.FullName.Substring($stageTask.Length+1).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
})
@{schema=1;version=$Version;backend='standalone';gameVersion='1.5.12620';files=$manifestFilesTask} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $stageTask 'release\native-package-manifest.json') -Encoding UTF8
# Explicit file list above excludes original game files, extracted assets, dependencies,
# all Java/JAR binaries, frame caches, accounts, logs and player saves.
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipTask=Join-Path $nativeRootTask ('artifacts\HollowCraft-Native-Windows-'+$Version+'.zip')
if(Test-Path -LiteralPath $zipTask){throw 'Package exists. Choose a new alpha version or archive it before packaging.'}
[IO.Compression.ZipFile]::CreateFromDirectory((Split-Path $stageTask -Parent),$zipTask,[IO.Compression.CompressionLevel]::Optimal,$false)
$hashTask=(Get-FileHash -LiteralPath $zipTask -Algorithm SHA256).Hash
[IO.File]::WriteAllText($zipTask+'.sha256',$hashTask.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($zipTask)+[Environment]::NewLine,[Text.UTF8Encoding]::new($false))
Write-Output $zipTask
Write-Output ('SHA256 '+$hashTask)
