param([string]$MinecraftJar,[string]$BetterCombatJar)
$ErrorActionPreference='Stop'
$nativeRootTask=Split-Path $PSScriptRoot -Parent
$assetsTask=Join-Path $nativeRootTask 'artifacts\StandaloneAssets'
$cacheTask=Join-Path $nativeRootTask 'dependencies\native-resources'
New-Item -ItemType Directory -Path $assetsTask,$cacheTask -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$lockTask=Get-Content -LiteralPath (Join-Path $nativeRootTask 'release\native-dependencies.lock.json') -Raw | ConvertFrom-Json
function NativeResourceArchiveTask($Metadata,[string]$Local,[string]$Filename) {
    $pathTask=if($Local){(Resolve-Path -LiteralPath $Local).Path}else{Join-Path $cacheTask $Filename}
    if(-not(Test-Path -LiteralPath $pathTask)) {
        [Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
        Write-Host ('Downloading static visual resources: '+$Filename)
        Invoke-WebRequest -UseBasicParsing -Uri $Metadata.url -OutFile ($pathTask+'.download') -TimeoutSec 120
        if((Get-FileHash -LiteralPath ($pathTask+'.download') -Algorithm SHA1).Hash -ne $Metadata.sha1){throw 'Visual resource checksum mismatch.'}
        Move-Item -LiteralPath ($pathTask+'.download') -Destination $pathTask
    }
    if((Get-FileHash -LiteralPath $pathTask -Algorithm SHA1).Hash -ne $Metadata.sha1){throw 'Visual archive version/checksum mismatch.'}
    return $pathTask
}
if(-not(Test-Path -LiteralPath (Join-Path $assetsTask 'native-visual-resources-v1.json'))) {
    $archiveTask=NativeResourceArchiveTask $lockTask.minecraftVisuals $MinecraftJar 'minecraft-visuals-1.20.1.jar'
    $zipTask=[IO.Compression.ZipFile]::OpenRead($archiveTask)
    $namedTask=@{
        'assets/minecraft/textures/entity/player/wide/steve.png'='steve_skin.png'
        'assets/minecraft/textures/font/ascii.png'='gui_font.png'
        'assets/minecraft/textures/entity/trident.png'='trident_model.png'
        'assets/minecraft/textures/entity/projectiles/arrow.png'='arrow_projectile.png'
        'assets/minecraft/textures/entity/elytra.png'='elytra_model.png'
        'assets/minecraft/textures/entity/shield_base.png'='shield_model.png'
        'assets/minecraft/textures/entity/chest/normal.png'='chest_model.png'
    }
    try {
        foreach($entryTask in $zipTask.Entries) {
            $nameTask=$null
            if($entryTask.FullName -match '^assets/minecraft/textures/(block|item)/([a-z0-9_]+)\.png$'){$nameTask=$Matches[2]+'.png'}
            elseif($entryTask.FullName -match '^assets/minecraft/textures/gui/(icons|widgets|container/[a-z0-9_]+)\.png$'){$nameTask='gui_'+($Matches[1] -replace 'container/','')+'.png'}
            elseif($entryTask.FullName -match '^assets/minecraft/textures/models/armor/([a-z0-9_]+)\.png$'){$nameTask='armor_'+$Matches[1]+'.png'}
            elseif($entryTask.FullName -match '^assets/minecraft/textures/mob_effect/([a-z0-9_]+)\.png$'){$nameTask='effect_'+$Matches[1]+'.png'}
            elseif($entryTask.FullName -match '^assets/minecraft/textures/gui/container/creative_inventory/([a-z0-9_]+)\.png$'){$nameTask='gui_creative_'+$Matches[1]+'.png'}
            elseif($entryTask.FullName -match '^assets/minecraft/textures/block/(water_still|lava_still|fire_0)\.png\.mcmeta$'){$nameTask=$Matches[1]+'.png.mcmeta'}
            else {$nameTask=$namedTask[$entryTask.FullName]}
            if($nameTask){$destinationTask=Join-Path $assetsTask $nameTask;if(-not(Test-Path -LiteralPath $destinationTask)){[IO.Compression.ZipFileExtensions]::ExtractToFile($entryTask,$destinationTask,$false)}}
        }
    }finally{$zipTask.Dispose()}
    foreach($aliasTask in @{'crafting_table'='crafting_table_side';'furnace'='furnace_front';'grass_block'='grass_block_side';'tnt'='tnt_side';'water'='water_still';'lava'='lava_still'}.GetEnumerator()){
        $destinationTask=Join-Path $assetsTask ($aliasTask.Key+'.png');if(-not(Test-Path -LiteralPath $destinationTask)){Copy-Item -LiteralPath (Join-Path $assetsTask ($aliasTask.Value+'.png')) -Destination $destinationTask}
    }
    @{schema=1;version='1.20.1';sha1=$lockTask.minecraftVisuals.sha1;source=$lockTask.minecraftVisuals.url;extracted='textures only; no game code loaded or executed'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $assetsTask 'native-visual-resources-v1.json') -Encoding UTF8
}
$combatTask=Join-Path $assetsTask 'bettercombat'
if(-not(Test-Path -LiteralPath (Join-Path $combatTask 'one_handed_slash_horizontal_right.json'))) {
    $archiveTask=NativeResourceArchiveTask $lockTask.betterCombatAnimations $BetterCombatJar 'better-combat-animation-resources.jar'
    $zipTask=[IO.Compression.ZipFile]::OpenRead($archiveTask);New-Item -ItemType Directory -Path $combatTask -Force | Out-Null
    try{foreach($entryTask in $zipTask.Entries){if($entryTask.FullName -match '^assets/bettercombat/attack_animations/([a-z0-9_]+)\.json$'){[IO.Compression.ZipFileExtensions]::ExtractToFile($entryTask,(Join-Path $combatTask ($Matches[1]+'.json')),$true)}}}finally{$zipTask.Dispose()}
}
if(-not(Test-Path -LiteralPath (Join-Path $assetsTask 'native-model-data-v2.json'))) {
    $archiveTask=NativeResourceArchiveTask $lockTask.minecraftVisuals $MinecraftJar 'minecraft-visuals-1.20.1.jar'
    $zipTask=[IO.Compression.ZipFile]::OpenRead($archiveTask)
    $countsTask=@{models=0;recipes=0;tags=0}
    try {foreach($entryTask in $zipTask.Entries) {
        $relativeTask=$null
        if($entryTask.FullName -match '^assets/minecraft/models/((item|block)/[a-z0-9_/]+\.json)$'){$relativeTask='models/minecraft/'+$Matches[1];$countsTask.models++}
        elseif($entryTask.FullName -match '^data/minecraft/recipes/([a-z0-9_/]+\.json)$'){$relativeTask='vanilla/recipes/'+$Matches[1];$countsTask.recipes++}
        elseif($entryTask.FullName -match '^data/minecraft/tags/items/([a-z0-9_/]+\.json)$'){$relativeTask='vanilla/tags/items/'+$Matches[1];$countsTask.tags++}
        if($relativeTask){$destinationTask=Join-Path $assetsTask $relativeTask;New-Item -ItemType Directory -Path (Split-Path $destinationTask -Parent) -Force | Out-Null;[IO.Compression.ZipFileExtensions]::ExtractToFile($entryTask,$destinationTask,$true)}
    }}finally{$zipTask.Dispose()}
    $countsTask | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $assetsTask 'native-model-data-v2.json') -Encoding UTF8
}
if(-not(Test-Path -LiteralPath (Join-Path $assetsTask 'bettercombat-profiles-v2.json'))) {
    $archiveTask=NativeResourceArchiveTask $lockTask.betterCombatAnimations $BetterCombatJar 'better-combat-animation-resources.jar'
    $zipTask=[IO.Compression.ZipFile]::OpenRead($archiveTask);$countTask=0
    try{foreach($entryTask in $zipTask.Entries){if($entryTask.FullName -match '^data/([a-z0-9_]+)/weapon_attributes/([a-z0-9_/]+\.json)$'){$destinationTask=Join-Path $assetsTask ('bettercombat_profiles/'+$Matches[1]+'/'+$Matches[2]);New-Item -ItemType Directory -Path (Split-Path $destinationTask -Parent) -Force | Out-Null;[IO.Compression.ZipFileExtensions]::ExtractToFile($entryTask,$destinationTask,$true);$countTask++}}}finally{$zipTask.Dispose()}
    @{profiles=$countTask} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $assetsTask 'bettercombat-profiles-v2.json') -Encoding UTF8
}
$registryTask=Join-Path $nativeRootTask 'release/native-registry.json'
if(-not(Test-Path -LiteralPath (Join-Path $assetsTask 'native-fluid-resources-v9.json'))) {
    $archiveTask=NativeResourceArchiveTask $lockTask.minecraftVisuals $MinecraftJar 'minecraft-visuals-1.20.1.jar';$zipTask=[IO.Compression.ZipFile]::OpenRead($archiveTask)
    try{foreach($fluidTask in @('water_still','water_flow','lava_still','lava_flow')){foreach($suffixTask in @('.png','.png.mcmeta')){$entryTask=$zipTask.GetEntry('assets/minecraft/textures/block/'+$fluidTask+$suffixTask);if(-not $entryTask){throw ('Missing fluid animation resource: '+$fluidTask+$suffixTask)};[IO.Compression.ZipFileExtensions]::ExtractToFile($entryTask,(Join-Path $assetsTask ($fluidTask+$suffixTask)),$true)}}}finally{$zipTask.Dispose()}
    @{schema=1;originalAnimationMetadata=$true} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $assetsTask 'native-fluid-resources-v9.json') -Encoding UTF8
}
Copy-Item -LiteralPath (Join-Path $nativeRootTask 'release/native-held-models.json') -Destination (Join-Path $assetsTask 'native-held-models.json') -Force
if(-not(Test-Path -LiteralPath (Join-Path $assetsTask 'native-launcher-resources-v8.json'))) {
    $archiveTask=NativeResourceArchiveTask $lockTask.minecraftVisuals $MinecraftJar 'minecraft-visuals-1.20.1.jar';$zipTask=[IO.Compression.ZipFile]::OpenRead($archiveTask)
    try{$entryTask=$zipTask.GetEntry('assets/minecraft/textures/entity/shield_base_nopattern.png');if(-not $entryTask){throw 'Missing original unpatterned shield texture'};[IO.Compression.ZipFileExtensions]::ExtractToFile($entryTask,(Join-Path $assetsTask 'shield_model.png'),$true)}finally{$zipTask.Dispose()}
    $languageLocalTask=@((Join-Path ${env:ProgramFiles(x86)} 'MC/.minecraft/assets'),(Join-Path $env:APPDATA '.minecraft/assets')) | Where-Object {Test-Path -LiteralPath (Join-Path $_ 'indexes/5.json')} | Select-Object -First 1
    $languageIndexTask=if($languageLocalTask){Join-Path $languageLocalTask 'indexes/5.json'}else{NativeResourceArchiveTask $lockTask.minecraftSoundsIndex $null 'minecraft-sounds-index-5.json'}
    $languageHashTask=(Get-Content -LiteralPath $languageIndexTask -Raw|ConvertFrom-Json).objects.'minecraft/lang/zh_cn.json'.hash
    if($languageHashTask -notmatch '^[a-f0-9]{40}$'){throw 'Missing language asset index record'}
    $languageObjectTask=if($languageLocalTask){Join-Path $languageLocalTask ('objects/'+$languageHashTask.Substring(0,2)+'/'+$languageHashTask)}else{Join-Path $cacheTask ('language-object-'+$languageHashTask)}
    if(-not(Test-Path -LiteralPath $languageObjectTask)){$languageObjectTask=Join-Path $cacheTask ('language-object-'+$languageHashTask);Invoke-WebRequest -UseBasicParsing -Uri ('https://resources.download.minecraft.net/'+$languageHashTask.Substring(0,2)+'/'+$languageHashTask) -OutFile $languageObjectTask -TimeoutSec 60}
    if((Get-FileHash -LiteralPath $languageObjectTask -Algorithm SHA1).Hash -ne $languageHashTask){throw 'Language asset checksum mismatch'}
    Copy-Item -LiteralPath $languageObjectTask -Destination (Join-Path $assetsTask 'native-zh_cn.json') -Force
    @{schema=1} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $assetsTask 'native-launcher-resources-v8.json') -Encoding UTF8
}
Copy-Item -LiteralPath (Join-Path $nativeRootTask 'release/native-trades.json') -Destination (Join-Path $assetsTask 'native-trades.json') -Force
if(-not(Test-Path -LiteralPath (Join-Path $assetsTask 'native-block-loot-v7.json'))) {
    $archiveTask=NativeResourceArchiveTask $lockTask.minecraftVisuals $MinecraftJar 'minecraft-visuals-1.20.1.jar';$zipTask=[IO.Compression.ZipFile]::OpenRead($archiveTask);$countTask=0
    try{foreach($entryTask in $zipTask.Entries){if($entryTask.FullName -match '^data/minecraft/loot_tables/blocks/([a-z0-9_/]+\.json)$'){$destinationTask=Join-Path $assetsTask ('vanilla/loot_tables/blocks/'+$Matches[1]);New-Item -ItemType Directory -Path (Split-Path $destinationTask -Parent) -Force | Out-Null;[IO.Compression.ZipFileExtensions]::ExtractToFile($entryTask,$destinationTask,$true);$countTask++}}}finally{$zipTask.Dispose()}
    @{tables=$countTask} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $assetsTask 'native-block-loot-v7.json') -Encoding UTF8
}
if(-not(Test-Path -LiteralPath (Join-Path $assetsTask 'native-entity-loot-v6.json'))) {
    $archiveTask=NativeResourceArchiveTask $lockTask.minecraftVisuals $MinecraftJar 'minecraft-visuals-1.20.1.jar';$zipTask=[IO.Compression.ZipFile]::OpenRead($archiveTask);$countTask=0
    try{foreach($entryTask in $zipTask.Entries){if($entryTask.FullName -match '^data/minecraft/loot_tables/entities/([a-z0-9_/]+\.json)$'){$destinationTask=Join-Path $assetsTask ('vanilla/loot_tables/entities/'+$Matches[1]);New-Item -ItemType Directory -Path (Split-Path $destinationTask -Parent) -Force | Out-Null;[IO.Compression.ZipFileExtensions]::ExtractToFile($entryTask,$destinationTask,$true);$countTask++}}}finally{$zipTask.Dispose()}
    @{tables=$countTask} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $assetsTask 'native-entity-loot-v6.json') -Encoding UTF8
}
$creatureModelsTask=Join-Path $nativeRootTask 'release/native-creature-models.json'
if(Test-Path -LiteralPath $creatureModelsTask) {
    Copy-Item -LiteralPath $creatureModelsTask -Destination (Join-Path $assetsTask 'native-creature-models.json') -Force
    $creatureHashTask=(Get-FileHash -LiteralPath $creatureModelsTask -Algorithm SHA256).Hash;$creatureMarkerTask=Join-Path $assetsTask 'native-creature-textures-v6.json';$creatureOldHashTask=if(Test-Path -LiteralPath $creatureMarkerTask){(Get-Content -LiteralPath $creatureMarkerTask -Raw | ConvertFrom-Json).sourceSha256}else{$null}
    if($creatureOldHashTask -ne $creatureHashTask) {
        $archiveTask=NativeResourceArchiveTask $lockTask.minecraftVisuals $MinecraftJar 'minecraft-visuals-1.20.1.jar';$zipTask=[IO.Compression.ZipFile]::OpenRead($archiveTask);$countTask=0
        $creatureDataTask=Get-Content -LiteralPath $creatureModelsTask -Raw | ConvertFrom-Json
        try{foreach($creatureTask in @($creatureDataTask.types)+@($creatureDataTask.boats)){foreach($layerTask in @($creatureTask)+@($creatureTask.layers)){
            if(-not $layerTask.sourceTexture){continue};$entryTask=$zipTask.GetEntry($layerTask.sourceTexture);if(-not $entryTask){throw ('Missing native entity texture: '+$layerTask.sourceTexture)}
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entryTask,(Join-Path $assetsTask ($layerTask.texture+'.png')),$true);$countTask++
        }}}finally{$zipTask.Dispose()}
        @{textures=$countTask;sourceSha256=$creatureHashTask} | ConvertTo-Json | Set-Content -LiteralPath $creatureMarkerTask -Encoding UTF8
    }
}
if(-not(Test-Path -LiteralPath (Join-Path $assetsTask 'native-font-v5.json'))) {
    $archiveTask=NativeResourceArchiveTask $lockTask.minecraftVisuals $MinecraftJar 'minecraft-visuals-1.20.1.jar'
    & (Join-Path $PSScriptRoot 'prepare-native-font.ps1') -MinecraftJar $archiveTask -AssetsRoot $assetsTask
}
if(-not(Test-Path -LiteralPath (Join-Path $assetsTask 'native-creature-textures-v4.json'))) {
    $archiveTask=NativeResourceArchiveTask $lockTask.minecraftVisuals $MinecraftJar 'minecraft-visuals-1.20.1.jar';$zipTask=[IO.Compression.ZipFile]::OpenRead($archiveTask);$countTask=0
    try{foreach($entryTask in $zipTask.Entries){if($entryTask.FullName -match '^assets/minecraft/textures/entity/(zombie/zombie|skeleton/skeleton|creeper/creeper|spider/spider|pig/pig|cow/cow|sheep/sheep|sheep/sheep_fur|chicken|villager/villager|boat/[a-z_]+)\.png$'){$nameTask='mob_'+$Matches[1].Replace('/','_')+'.png';[IO.Compression.ZipFileExtensions]::ExtractToFile($entryTask,(Join-Path $assetsTask $nameTask),$true);$countTask++}}}finally{$zipTask.Dispose()};@{textures=$countTask} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $assetsTask 'native-creature-textures-v4.json') -Encoding UTF8
}
if(Test-Path -LiteralPath $registryTask){Copy-Item -LiteralPath $registryTask -Destination (Join-Path $assetsTask 'native-registry.json') -Force}
if(-not(Test-Path -LiteralPath (Join-Path $assetsTask 'native-audio-v4.json'))){try{$audioMinecraftTask=NativeResourceArchiveTask $lockTask.minecraftVisuals $MinecraftJar 'minecraft-visuals-1.20.1.jar';$audioCombatTask=NativeResourceArchiveTask $lockTask.betterCombatAnimations $BetterCombatJar 'better-combat-animation-resources.jar';& (Join-Path $PSScriptRoot 'prepare-native-audio.ps1') -MinecraftJar $audioMinecraftTask -BetterCombatJar $audioCombatTask}catch{Write-Warning ('Optional native sound preparation failed: '+$_.Exception.Message)}}
Write-Output 'Static textures, model transforms, combat profiles and recipe data ready. Minecraft/Java was not started.'
