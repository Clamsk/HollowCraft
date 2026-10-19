$ErrorActionPreference='Stop'
$repoTask=Split-Path $PSScriptRoot -Parent
. (Join-Path $repoTask 'scripts/native-launcher-settings.ps1')
$fixtureTask=Join-Path $repoTask ('artifacts/native-launcher-settings-checks/'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureTask -Force|Out-Null
$indexTask=@{}
foreach($itemTask in (Get-Content -LiteralPath (Join-Path $repoTask 'release/native-registry.json') -Raw -Encoding UTF8|ConvertFrom-Json).items){$indexTask[$itemTask.id]=$itemTask}
$indexTask['hollowcraft_bridge:zenith']=[pscustomobject]@{maxCount=1;equipment=''}
$indexTask['hollowcraft_bridge:stardust_wings']=[pscustomobject]@{maxCount=1;equipment='chest'}
$script:checksTask=0
function CheckSettingsTask([bool]$Condition,[string]$Reason){if(-not $Condition){throw $Reason};$script:checksTask++}
foreach($presetTask in @('classic','light','ranger','netherite','clamsk','empty')){
    $pathTask=Join-Path $fixtureTask ($presetTask+'.json');$configTask=Read-HollowCraftSettingsTask $pathTask;$configTask.loadout=New-HollowCraftLoadoutTask $presetTask
    Save-HollowCraftSettingsTask $pathTask $configTask $indexTask;$copyTask=Read-HollowCraftSettingsTask $pathTask
    CheckSettingsTask ($copyTask.loadout.slots.Count -eq 36 -and $copyTask.loadout.armor.Count -eq 4 -and $copyTask.loadoutRevision) ('Preset round-trip: '+$presetTask)
    if($presetTask -eq 'empty'){CheckSettingsTask (@($copyTask.loadout.slots|Where-Object count -gt 0).Count -eq 0 -and @($copyTask.loadout.armor|Where-Object count -gt 0).Count -eq 0) 'Empty preset stays empty'}
    if($presetTask -eq 'clamsk'){CheckSettingsTask ($copyTask.loadout.slots[0].id -eq 'hollowcraft_bridge:zenith' -and $copyTask.loadout.armor[1].id -eq 'hollowcraft_bridge:stardust_wings') 'Easter-egg gear fits the real chest slot'}
}
$pathTask=Join-Path $fixtureTask 'idempotence.json';$configTask=Read-HollowCraftSettingsTask $pathTask;$configTask.applyLoadoutToExisting=$true
Save-HollowCraftSettingsTask $pathTask $configTask $indexTask;$revisionTask=$configTask.loadoutRevision
Save-HollowCraftSettingsTask $pathTask $configTask $indexTask
CheckSettingsTask ($revisionTask -eq $configTask.loadoutRevision) 'Repeated Play/save cannot replace an existing backpack again without a kit change'
$configTask.keepInventory=$false;$configTask.waterReturnTicks=500;Save-HollowCraftSettingsTask $pathTask $configTask $indexTask
CheckSettingsTask ($revisionTask -eq $configTask.loadoutRevision -and $configTask.waterReturnTicks -eq 120) 'Changing survival rules preserves kit revision and bounds the water delay'
$configTask.loadout.slots[0]=New-HollowCraftStackTask 'diamond_sword' 64;Save-HollowCraftSettingsTask $pathTask $configTask $indexTask
CheckSettingsTask ($revisionTask -ne $configTask.loadoutRevision -and $configTask.loadout.slots[0].count -eq 1) 'A changed kit creates a revision and clamps unstackable tools'
$configTask.loadout.armor[0]=New-HollowCraftStackTask 'diamond_sword' 1;$rejectedTask=$false
try{Save-HollowCraftSettingsTask $pathTask $configTask $indexTask}catch{$rejectedTask=$true}
CheckSettingsTask $rejectedTask 'Non-armor cannot be saved into a helmet slot'
CheckSettingsTask ((Read-HollowCraftSettingsTask $pathTask).loadout.armor[0].id -eq 'minecraft:iron_helmet') 'Rejected edits leave the prior file intact'
# Construct and render our own WPF UI offscreen; never invoke Play/Install/Close.
. (Join-Path $repoTask 'scripts/launcher-native.ps1') -PreviewPath (Join-Path $fixtureTask 'ui.png') -PreviewTab 1
$script:settingsPathTask=Join-Path $fixtureTask 'ui-settings.json';$script:settingsTask=Read-HollowCraftSettingsTask $script:settingsPathTask
$buttonTask=$script:controlsTask.HotbarSlots.Children[3];$buttonTask.RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
CheckSettingsTask ($script:targetTask -eq 3 -and $script:controlsTask.MainTabs.SelectedItem -eq $script:controlsTask.CatalogTab) 'Selecting a hotbar slot opens the picker with that actual destination'
$script:controlsTask.Search.Text='钻石剑';$script:controlsTask.Quantity.Text='64';$buttonTask=@($script:controlsTask.CatalogGrid.Children|Where-Object {$_.Tag.id -eq 'minecraft:diamond_sword'})[0]
CheckSettingsTask ($null -ne $buttonTask) 'Chinese item search resolves original translations'
$buttonTask.RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
CheckSettingsTask ($script:settingsTask.loadout.slots[3].id -eq 'minecraft:diamond_sword' -and $script:settingsTask.loadout.slots[3].count -eq 1) 'Picker click transfers a clamped stack to the selected slot'
$script:controlsTask.SaveButton.RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
CheckSettingsTask ((Read-HollowCraftSettingsTask $script:settingsPathTask).loadout.slots[3].id -eq 'minecraft:diamond_sword') 'Save button writes the reviewed kit to its configured path'
Write-Output ('Native launcher settings/UI checks: '+$script:checksTask+' passed. No game was launched or closed.')
