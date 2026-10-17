param([Parameter(Mandatory=$true)][string]$AssetsRoot)
$ErrorActionPreference='Stop'
$nativeRootTask=Split-Path $PSScriptRoot -Parent
$sourceTask=(Resolve-Path -LiteralPath $AssetsRoot).Path
$targetTask=Join-Path $nativeRootTask 'artifacts\StandaloneAssets'
New-Item -ItemType Directory -Path $targetTask -Force | Out-Null
if($sourceTask -eq $targetTask){throw 'Select a separate source resource folder.'}
$countTask=0
# Static local resources only; no live player/entity/GUI frames, saves or executables.
foreach($fileTask in Get-ChildItem -LiteralPath $sourceTask -File) {
    if($fileTask.Name -notmatch '^[a-zA-Z0-9_.-]+\.(png|mcmeta)$'){continue}
    Copy-Item -LiteralPath $fileTask.FullName -Destination $targetTask -Force;$countTask++
}
foreach($folderTask in @('bettercombat','terraria','scene_models','scene_models_v2','item_models_rgba')) {
    $directoryTask=Join-Path $sourceTask $folderTask
    if(-not(Test-Path -LiteralPath $directoryTask)){continue}
    foreach($fileTask in Get-ChildItem -LiteralPath $directoryTask -Recurse -File) {
        if($fileTask.Extension -notin '.json','.png','.wav','.ogg','.txt'){continue}
        $relativeTask=$fileTask.FullName.Substring($sourceTask.TrimEnd('\').Length+1)
        $destinationTask=Join-Path $targetTask $relativeTask
        New-Item -ItemType Directory -Path (Split-Path $destinationTask -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $fileTask.FullName -Destination $destinationTask -Force;$countTask++
    }
}
Write-Output ("Imported $countTask static resources locally. Restart Native HollowCraft to use them. These files are excluded from release packages.")
