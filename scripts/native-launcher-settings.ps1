function New-HollowCraftStackTask([string]$Id='minecraft:air',[int]$Count=0,$Nbt=$null) {
    if($Id -notmatch ':'){$Id='minecraft:'+$Id};if($null -eq $Nbt){$Nbt=[pscustomobject]@{}}
    return [pscustomobject]@{id=$Id;count=$Count;damage=0;nbt=$Nbt}
}
function New-HollowCraftLoadoutTask([string]$Preset='classic') {
    $slotsTask=@(0..35|ForEach-Object {New-HollowCraftStackTask});$armorTask=@(0..3|ForEach-Object {New-HollowCraftStackTask});$offhandTask=New-HollowCraftStackTask
    $hotbarTask=@('iron_sword','bow','golden_apple','cobblestone','water_bucket','lava_bucket','tnt','flint_and_steel','ender_pearl');$countsTask=@(1,1,64,64,1,1,64,1,16)
    if($Preset -eq 'empty'){return [pscustomobject]@{slots=$slotsTask;armor=$armorTask;offhand=$offhandTask}}
    if($Preset -eq 'light'){$hotbarTask=@('stone_sword','stone_pickaxe','bread','cobblestone','water_bucket','torch','oak_log','stone_axe','ender_pearl');$countsTask=@(1,1,16,64,1,32,32,1,8)}
    if($Preset -eq 'ranger'){$hotbarTask=@('bow','iron_sword','golden_apple','cobblestone','water_bucket','cooked_beef','torch','iron_pickaxe','ender_pearl');$countsTask=@(1,1,16,64,1,64,64,1,16)}
    if($Preset -eq 'netherite'){$hotbarTask[0]='netherite_sword'}
    if($Preset -eq 'clamsk'){$hotbarTask[0]='hollowcraft_bridge:zenith'}
    for($iTask=0;$iTask -lt 9;$iTask++){$slotsTask[$iTask]=New-HollowCraftStackTask $hotbarTask[$iTask] $countsTask[$iTask]}
    $supplyTask=@(@('arrow',64),@('cooked_beef',64),@('cobblestone',64),@('cobblestone',64),@('oak_log',64),@('shield',1),@('iron_axe',1),@('iron_pickaxe',1),@('torch',64),@('iron_ingot',64),@('bedrock',64),@('totem_of_undying',1),@('totem_of_undying',1),@('oak_boat',1))
    if($Preset -eq 'light'){$supplyTask=@(@('iron_ingot',16),@('cooked_beef',16))}
    if($Preset -eq 'ranger'){$supplyTask=@(@('arrow',64),@('arrow',64),@('spectral_arrow',64),@('cooked_beef',64),@('totem_of_undying',1));$offhandTask=New-HollowCraftStackTask 'shield' 1}
    for($iTask=0;$iTask -lt $supplyTask.Count;$iTask++){$slotsTask[9+$iTask]=New-HollowCraftStackTask $supplyTask[$iTask][0] $supplyTask[$iTask][1]}
    $materialTask=if($Preset -eq 'light'){'leather'}elseif($Preset -eq 'netherite'){'netherite'}else{'iron'};$partsTask=@('helmet','chestplate','leggings','boots')
    for($iTask=0;$iTask -lt 4;$iTask++){$armorTask[$iTask]=New-HollowCraftStackTask ($materialTask+'_'+$partsTask[$iTask]) 1}
    if($Preset -eq 'clamsk'){$armorTask[1]=New-HollowCraftStackTask 'hollowcraft_bridge:stardust_wings' 1;$offhandTask=New-HollowCraftStackTask 'shield' 1}
    return [pscustomobject]@{slots=$slotsTask;armor=$armorTask;offhand=$offhandTask}
}
function Read-HollowCraftSettingsTask([string]$Path) {
    $settingsTask=[pscustomobject]@{schema=1;keepInventory=$true;automaticWater=$true;waterReturnTicks=40;applyLoadoutToExisting=$false;loadoutRevision='';loadout=(New-HollowCraftLoadoutTask)}
    if(Test-Path -LiteralPath $Path){$existingTask=Get-Content -LiteralPath $Path -Raw -Encoding UTF8|ConvertFrom-Json;if($existingTask.schema -ne 1){throw 'Unknown launcher settings schema'};foreach($propertyTask in $settingsTask.PSObject.Properties.Name){if($null -ne $existingTask.$propertyTask){$settingsTask.$propertyTask=$existingTask.$propertyTask}}};return $settingsTask
}
function Save-HollowCraftSettingsTask([string]$Path,$Settings,$ItemIndex) {
    if($Settings.loadout.slots.Count -ne 36 -or $Settings.loadout.armor.Count -ne 4){throw 'Invalid equipment layout'}
    $allTask=@($Settings.loadout.slots)+@($Settings.loadout.armor)+@($Settings.loadout.offhand)
    foreach($stackTask in $allTask){if($stackTask.count -le 0){$stackTask.id='minecraft:air';$stackTask.count=0;continue};if(-not $ItemIndex.ContainsKey($stackTask.id)){throw ('Unknown item: '+$stackTask.id)};$stackTask.count=[Math]::Max(1,[Math]::Min([int]$stackTask.count,[int]$ItemIndex[$stackTask.id].maxCount))}
    for($slotTask=0;$slotTask -lt 4;$slotTask++){$pieceTask=$Settings.loadout.armor[$slotTask];if($pieceTask.count -gt 0 -and $ItemIndex[$pieceTask.id].equipment -ne @('head','chest','legs','feet')[$slotTask]){throw 'Item does not fit armor slot'}}
    $Settings.waterReturnTicks=[Math]::Max(20,[Math]::Min(120,[int]$Settings.waterReturnTicks))
    $previousTask=if(Test-Path -LiteralPath $Path){Read-HollowCraftSettingsTask $Path}else{$null}
    $sameKitTask=$null -ne $previousTask -and (($previousTask.loadout|ConvertTo-Json -Depth 64 -Compress) -eq ($Settings.loadout|ConvertTo-Json -Depth 64 -Compress))
    if($sameKitTask -and $previousTask.loadoutRevision -and (-not $Settings.applyLoadoutToExisting -or $previousTask.applyLoadoutToExisting)){$Settings.loadoutRevision=$previousTask.loadoutRevision}else{$Settings.loadoutRevision=[Guid]::NewGuid().ToString('N')}
    $temporaryTask=$Path+'.tmp';[IO.File]::WriteAllText($temporaryTask,($Settings|ConvertTo-Json -Depth 64),[Text.UTF8Encoding]::new($false));if(Test-Path -LiteralPath $Path){[IO.File]::Replace($temporaryTask,$Path,($Path+'.bak'))}else{[IO.File]::Move($temporaryTask,$Path)}
}
