param([string]$Version='0.30.15')
$ErrorActionPreference='Stop'
$rootTask=Split-Path $PSScriptRoot -Parent
$dllTask=Join-Path $rootTask 'artifacts\HollowCraft\HollowCraft.dll'
if([Reflection.AssemblyName]::GetAssemblyName($dllTask).Version.ToString(3) -ne $Version) {throw 'HollowCraft.dll version does not match the release.'}
$jarTask=Join-Path $rootTask 'minecraft\build\libs\hollowcraft-minecraft-0.1.0.jar'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$jarZipTask=[IO.Compression.ZipFile]::OpenRead($jarTask)
try {
    $readerTask=[IO.StreamReader]::new($jarZipTask.GetEntry('fabric.mod.json').Open())
    try {$modTask=$readerTask.ReadToEnd() | ConvertFrom-Json} finally {$readerTask.Dispose()}
    if($modTask.version -ne $Version) {throw 'Minecraft mod version does not match the release.'}
} finally {$jarZipTask.Dispose()}
$stageTask=Join-Path $rootTask ('artifacts\release-staging\'+[Guid]::NewGuid().ToString('N')+'\HollowCraft')
New-Item -ItemType Directory -Path $stageTask -Force | Out-Null
# Explicit allowlist: never copy game binaries, extracted assets, runtime or saves.
$filesTask=@(
    'Install HollowCraft.cmd','Start HollowCraft.cmd','Stop HollowCraft.cmd','HollowCraft Launcher.cmd','Boss Recording.cmd',
    'scripts\hollowcraft-common.ps1','scripts\install-hollowcraft.ps1',
    'scripts\build-hollow-knight.ps1','scripts\prepare-assets.ps1',
    'scripts\prepare-hk-test.ps1','scripts\prepare-mc-test.ps1',
    'scripts\start-hollowcraft.ps1','scripts\stop-hollowcraft.ps1',
    'scripts\launcher.ps1',
    'scripts\audit-hk-enemies.py',
    'scripts\prepare-terraria-assets.ps1',
    'tools\TerrariaAssetImporter\Importer.cs','tools\TerrariaAssetImporter\LzxDecoder.cs',
    'tools\TerrariaAssetImporter\LzxDecoderStream.cs','tools\TerrariaAssetImporter\LICENSE.txt',
    'tools\TerrariaAssetImporter\SOURCE.md',
    'release\dependencies.lock.json','release\hollowcraft.example.json',
    'release\bosses.json',
    'release\creatures.json',
    'release\THIRD_PARTY.md','release\THIRD_PARTY.en.md',
    'release\INSTALL_zh.md','release\INSTALL_en.md',
    'README.md','README.en.md','SOURCE_SETUP.md','SOURCE_SETUP.en.md',
    'docs\ARCHITECTURE.md','docs\ARCHITECTURE.en.md',
    'docs\PLAYTHROUGH.md','docs\PLAYTHROUGH.en.md',
    'docs\BOSS_RECORDING.md','docs\BOSS_RECORDING.en.md',
    'docs\MC_CREATURES.md','docs\MC_CREATURES.en.md',
    'docs\PERFORMANCE.md','docs\PERFORMANCE.en.md',
    'docs\TERRARIA_EASTER_EGG.md','docs\TERRARIA_EASTER_EGG.en.md',
    'docs\ENEMY_AUDIT.md','docs\ENEMY_AUDIT.en.md','docs\enemy-coverage-0.30.8.csv',
    'artifacts\HollowCraft\HollowCraft.dll',
    'minecraft\build\libs\hollowcraft-minecraft-0.1.0.jar'
)
foreach($relativeTask in $filesTask) {
    $targetTask=Join-Path $stageTask $relativeTask
    New-Item -ItemType Directory -Path (Split-Path $targetTask -Parent) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $rootTask $relativeTask) -Destination $targetTask
}
Copy-Item -LiteralPath (Join-Path $rootTask 'release\INSTALL_zh.md') -Destination (Join-Path $stageTask 'INSTALL_zh.md')
Copy-Item -LiteralPath (Join-Path $rootTask 'release\INSTALL_en.md') -Destination (Join-Path $stageTask 'INSTALL_en.md')
foreach($installNameTask in @('INSTALL_zh.md','INSTALL_en.md')) {
    $installPathTask=Join-Path $stageTask $installNameTask
    $installTextTask=[IO.File]::ReadAllText($installPathTask).Replace('(../docs/','(docs/')
    [IO.File]::WriteAllText($installPathTask,$installTextTask,[Text.UTF8Encoding]::new($false))
}
# Find the Unicode playtest note without placing non-ASCII literals in PS5 scripts.
$noteVersionTask=if($Version.EndsWith('.0')) {$Version.Substring(0,$Version.Length-2)}else{$Version}
$noteTask=Get-ChildItem -LiteralPath $rootTask -File | Where-Object {$_.Name -like ('*-'+$noteVersionTask+'.md')} | Select-Object -First 1
if($noteTask) {
    Copy-Item -LiteralPath $noteTask.FullName -Destination (Join-Path $stageTask 'CHANGELOG_zh.md')
    Copy-Item -LiteralPath $noteTask.FullName -Destination (Join-Path $stageTask $noteTask.Name)
}
$englishNoteTask=Join-Path $rootTask ('PLAYTEST-'+$noteVersionTask+'.en.md')
if(Test-Path -LiteralPath $englishNoteTask) {
    Copy-Item -LiteralPath $englishNoteTask -Destination (Join-Path $stageTask 'CHANGELOG_en.md')
    Copy-Item -LiteralPath $englishNoteTask -Destination (Join-Path $stageTask ([IO.Path]::GetFileName($englishNoteTask)))
}
$manifestFilesTask=@()
foreach($fileTask in Get-ChildItem -LiteralPath $stageTask -File -Recurse | Sort-Object FullName) {
    $relativeTask=$fileTask.FullName.Substring($stageTask.Length+1).Replace('\','/')
    $manifestFilesTask+=@{path=$relativeTask;sha256=(Get-FileHash -LiteralPath $fileTask.FullName -Algorithm SHA256).Hash.ToLowerInvariant();bytes=$fileTask.Length}
}
@{schema=1;version=$Version;files=$manifestFilesTask} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $stageTask 'release\package-manifest.json') -Encoding UTF8
$releaseDirTask=Join-Path $rootTask 'artifacts\releases'
New-Item -ItemType Directory -Path $releaseDirTask -Force | Out-Null
$outputTask=Join-Path $releaseDirTask ('HollowCraft-v'+$Version+'-windows.zip')
if(Test-Path -LiteralPath $outputTask) {throw 'Release ZIP already exists. Choose a new version or move the old ZIP before packaging.'}
[IO.Compression.ZipFile]::CreateFromDirectory((Split-Path $stageTask -Parent),$outputTask,[IO.Compression.CompressionLevel]::Optimal,$false)
$hashTask=(Get-FileHash -LiteralPath $outputTask -Algorithm SHA256).Hash.ToLowerInvariant()
($hashTask+'  '+[IO.Path]::GetFileName($outputTask)) | Set-Content -LiteralPath (Join-Path $releaseDirTask ('HollowCraft-v'+$Version+'-SHA256.txt')) -Encoding ASCII
Write-Output ('Release package: '+$outputTask)
Write-Output ('SHA256: '+$hashTask)
Write-Output ('Staging folder: '+$stageTask)
