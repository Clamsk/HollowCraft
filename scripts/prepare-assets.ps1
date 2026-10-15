param([string]$MinecraftJar,[string]$BetterCombatJar)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'hollowcraft-common.ps1')
$configTask=Get-HollowCraftConfigTask
if(-not $MinecraftJar) {$MinecraftJar=Join-Path $configTask.minecraftRoot 'versions\1.20.1\1.20.1.jar'}
if(-not $BetterCombatJar) {
    if($configTask.portable) {
        $lockTask=Get-Content -LiteralPath (Join-Path $hollowCraftTaskRoot 'release\dependencies.lock.json') -Raw -Encoding UTF8 | ConvertFrom-Json
        $combatFileTask=($lockTask.mods | Where-Object project -EQ 'better-combat').filename
        $BetterCombatJar=Join-Path $hollowCraftTaskRoot ('dependencies\mods\'+$combatFileTask)
    } else {$BetterCombatJar=Join-Path $configTask.minecraftRoot 'versions\DarkRPG - RPG Quest Magic & Origins Online Adventure\mods\bettercombat-fabric-1.8.5+1.20.1.jar'}
}
$projectTaskRoot=Split-Path $PSScriptRoot -Parent
$assetsTask=Join-Path $projectTaskRoot 'artifacts\HollowCraft\HollowCraftAssets'
New-Item -ItemType Directory -Path $assetsTask -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipTask=[IO.Compression.ZipFile]::OpenRead($MinecraftJar)
try {
    foreach ($entryTask in $zipTask.Entries) {
        if($entryTask.FullName -match '^assets/minecraft/textures/block/(water_still|lava_still|fire_0)\.png\.mcmeta$') {[IO.Compression.ZipFileExtensions]::ExtractToFile($entryTask,(Join-Path $assetsTask ($Matches[1]+'.png.mcmeta')),$true)}
        if($entryTask.FullName -eq 'assets/minecraft/textures/entity/trident.png') {[IO.Compression.ZipFileExtensions]::ExtractToFile($entryTask,(Join-Path $assetsTask 'trident_model.png'),$true)}
        if($entryTask.FullName -eq 'assets/minecraft/textures/entity/projectiles/arrow.png') {[IO.Compression.ZipFileExtensions]::ExtractToFile($entryTask,(Join-Path $assetsTask 'arrow_projectile.png'),$true)}
        if($entryTask.FullName -eq 'assets/minecraft/textures/entity/elytra.png') {[IO.Compression.ZipFileExtensions]::ExtractToFile($entryTask,(Join-Path $assetsTask 'elytra_model.png'),$true)}
        if($entryTask.FullName -match '^assets/minecraft/textures/mob_effect/([a-z0-9_]+)\.png$') {[IO.Compression.ZipFileExtensions]::ExtractToFile($entryTask,(Join-Path $assetsTask ('effect_'+$Matches[1]+'.png')),$true)}
        if($entryTask.FullName -eq 'assets/minecraft/textures/entity/chest/normal.png') {[IO.Compression.ZipFileExtensions]::ExtractToFile($entryTask,(Join-Path $assetsTask 'chest_model.png'),$true)}
        if($entryTask.FullName -eq 'assets/minecraft/textures/entity/shield_base.png') {[IO.Compression.ZipFileExtensions]::ExtractToFile($entryTask,(Join-Path $assetsTask 'shield_model.png'),$true)}
        if($entryTask.FullName -match '^assets/minecraft/textures/gui/container/creative_inventory/([a-z0-9_]+)\.png$') {
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entryTask,(Join-Path $assetsTask ('gui_creative_'+$Matches[1]+'.png')),$true)
        }
        if($entryTask.FullName -match '^assets/minecraft/textures/gui/(icons|widgets|container/[a-z0-9_]+)\.png$') {
            $guiNameTask='gui_'+($Matches[1] -replace 'container/','')+'.png'
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entryTask,(Join-Path $assetsTask $guiNameTask),$true)
        }
        if($entryTask.FullName -match '^assets/minecraft/textures/models/armor/([a-z0-9_]+)\.png$') {
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entryTask,(Join-Path $assetsTask ('armor_'+$Matches[1]+'.png')),$true)
        }
        if($entryTask.FullName -eq 'assets/minecraft/textures/font/ascii.png') {[IO.Compression.ZipFileExtensions]::ExtractToFile($entryTask,(Join-Path $assetsTask 'gui_font.png'),$true)}
        if($entryTask.FullName -eq 'assets/minecraft/textures/entity/player/wide/steve.png') {
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entryTask,(Join-Path $assetsTask 'steve_skin.png'),$true)
        }
        if ($entryTask.FullName -match '^assets/minecraft/textures/(block|item)/([a-z0-9_]+)\.png$') {
            $assetNameTask=$Matches[2]+'.png'
            $targetTask=Join-Path $assetsTask $assetNameTask
            if (-not (Test-Path -LiteralPath $targetTask)) { [IO.Compression.ZipFileExtensions]::ExtractToFile($entryTask,$targetTask,$false) }
        }
    }
    foreach ($aliasTask in @{'oak_log'='oak_log';'crafting_table'='crafting_table_side';'furnace'='furnace_front';'grass_block'='grass_block_side';'tnt'='tnt_side';'water'='water_still';'lava'='lava_still'}.GetEnumerator()) {
        $sourceTask=Join-Path $assetsTask ($aliasTask.Value+'.png')
        $targetTask=Join-Path $assetsTask ($aliasTask.Key+'.png')
        if ($sourceTask -ne $targetTask -and (Test-Path -LiteralPath $sourceTask)) { Copy-Item -LiteralPath $sourceTask -Destination $targetTask -Force }
    }
} finally { $zipTask.Dispose() }
$combatAnimationsTask=Join-Path $assetsTask 'bettercombat'
New-Item -ItemType Directory -Path $combatAnimationsTask -Force | Out-Null
$combatJarTask=[IO.Compression.ZipFile]::OpenRead($BetterCombatJar)
try {
    foreach($entryTask in $combatJarTask.Entries) {
        if($entryTask.FullName -match '^assets/bettercombat/attack_animations/([a-z0-9_]+)\.json$') {
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entryTask,(Join-Path $combatAnimationsTask ($Matches[1]+'.json')),$true)
        }
    }
} finally {$combatJarTask.Dispose()}
Write-Output "Extracted textures from your local Minecraft installation: $assetsTask"
& (Join-Path $PSScriptRoot 'prepare-terraria-assets.ps1') -AssetsRoot $assetsTask
