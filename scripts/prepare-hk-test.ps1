param([string]$GameRoot='C:\Program Files (x86)\Steam\steamapps\common\Hollow Knight')
$ErrorActionPreference='Stop'
$projectTaskRoot=Split-Path $PSScriptRoot -Parent
$gameTestTask=Join-Path $projectTaskRoot 'runtime\Hollow Knight'
New-Item -ItemType Directory -Path $gameTestTask -Force | Out-Null
# Links only to static game resources. The patched assembly, plugin and logs are private copies.
function Link-ReadOnlyResourcesTask([string]$Source,[string]$Destination,[string[]]$Excluded=@()) {
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    foreach($itemTask in Get-ChildItem -LiteralPath $Source -Force) {
        if ($Excluded -contains $itemTask.Name) { continue }
        $targetTask=Join-Path $Destination $itemTask.Name
        if (Test-Path -LiteralPath $targetTask) { continue }
        if ($itemTask.PSIsContainer) { New-Item -ItemType Junction -Path $targetTask -Target $itemTask.FullName | Out-Null }
        else { New-Item -ItemType HardLink -Path $targetTask -Target $itemTask.FullName | Out-Null }
    }
}
Link-ReadOnlyResourcesTask $GameRoot $gameTestTask @('hollow_knight_Data','hollow_knight.exe','UnityPlayer.dll')
Copy-Item -LiteralPath (Join-Path $GameRoot 'hollow_knight.exe') -Destination $gameTestTask -Force
$unityTestTask=Join-Path $gameTestTask 'UnityPlayer.dll'
if (Test-Path -LiteralPath $unityTestTask) { Remove-Item -LiteralPath $unityTestTask }
Copy-Item -LiteralPath (Join-Path $GameRoot 'UnityPlayer.dll') -Destination $unityTestTask
Set-Content -LiteralPath (Join-Path $gameTestTask 'steam_appid.txt') -Value '367520' -Encoding ascii
$testDataTask=Join-Path $gameTestTask 'hollow_knight_Data'
Link-ReadOnlyResourcesTask (Join-Path $GameRoot 'hollow_knight_Data') $testDataTask @('Managed')
$testManagedTask=Join-Path $testDataTask 'Managed'
Link-ReadOnlyResourcesTask (Join-Path $GameRoot 'hollow_knight_Data\Managed') $testManagedTask @('Assembly-CSharp.dll')
$artifactTask=Join-Path $projectTaskRoot 'artifacts\HollowCraft'
Copy-Item -LiteralPath (Join-Path $artifactTask 'Assembly-CSharp.dll') -Destination $testManagedTask -Force
Copy-Item -LiteralPath (Join-Path $artifactTask 'HollowCraft.dll') -Destination $testManagedTask -Force
$testAssetsTask=Join-Path $testManagedTask 'HollowCraftAssets'
if (-not (Test-Path -LiteralPath $testAssetsTask)) { New-Item -ItemType Junction -Path $testAssetsTask -Target (Join-Path $artifactTask 'HollowCraftAssets') | Out-Null }
Write-Output "Isolated HollowCraft game prepared: $gameTestTask"
