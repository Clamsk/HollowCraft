param([switch]$ValidateOnly,[string]$PreviewPath,[ValidateRange(0,3)][int]$PreviewTab=0)
$ErrorActionPreference='Stop'
$nativeRootTask=Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'native-launcher-process.ps1')
. (Join-Path $PSScriptRoot 'native-launcher-settings.ps1')
$catalogTask=Get-Content -LiteralPath (Join-Path $nativeRootTask 'release/bosses.json') -Raw -Encoding UTF8|ConvertFrom-Json
$registryTask=Get-Content -LiteralPath (Join-Path $nativeRootTask 'release/native-registry.json') -Raw -Encoding UTF8|ConvertFrom-Json
if($catalogTask.schema -ne 1){throw 'Unsupported boss catalogue'}
if($ValidateOnly){Write-Output ('Native launcher: '+$catalogTask.items.Count+' bosses; '+$registryTask.items.Count+' item records; loadout and survival settings ready.');return}
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Web.Extensions
Add-Type -Path (Join-Path $nativeRootTask 'release/launcher/NativeLauncherIcons.cs') -ReferencedAssemblies @('System.Web.Extensions','System.Xaml',[Windows.Media.Imaging.BitmapSource].Assembly.Location,[Windows.DependencyObject].Assembly.Location)
$script:settingsPathTask=Join-Path $nativeRootTask 'hollowcraft.native.settings.json'
$script:settingsTask=Read-HollowCraftSettingsTask $script:settingsPathTask
$script:itemIndexTask=@{}
foreach($itemTask in $registryTask.items){$script:itemIndexTask[$itemTask.id]=$itemTask}
$script:itemIndexTask['hollowcraft_bridge:zenith']=[pscustomobject]@{id='hollowcraft_bridge:zenith';maxCount=1;equipment=''}
$script:itemIndexTask['hollowcraft_bridge:stardust_wings']=[pscustomobject]@{id='hollowcraft_bridge:stardust_wings';maxCount=1;equipment='chest'}
$script:assetsTask=Join-Path $nativeRootTask 'artifacts/StandaloneAssets'
$script:launcherIconsTask=[NativeLauncherIcons]::new($script:assetsTask)
$script:translationsTask=@{};$translationTask=Join-Path $script:assetsTask 'native-zh_cn.json'
if(Test-Path -LiteralPath $translationTask){$translationDataTask=Get-Content -LiteralPath $translationTask -Raw -Encoding UTF8|ConvertFrom-Json;foreach($propertyTask in $translationDataTask.PSObject.Properties){$script:translationsTask[$propertyTask.Name]=[string]$propertyTask.Value}}
[xml]$xamlTask=[IO.File]::ReadAllText((Join-Path $nativeRootTask 'release/launcher/launcher.xaml'))
$readerTask=New-Object Xml.XmlNodeReader $xamlTask
$script:windowTask=[Windows.Markup.XamlReader]::Load($readerTask)
$script:controlsTask=@{}
foreach($nodeTask in $xamlTask.SelectNodes('//*[@*[local-name()="Name"]]')){$nameTask=$nodeTask.GetAttribute('Name','http://schemas.microsoft.com/winfx/2006/xaml');$script:controlsTask[$nameTask]=$windowTask.FindName($nameTask)}
$script:controlsTask.Backdrop.Source=[Windows.Media.Imaging.BitmapImage]::new([Uri](Join-Path $nativeRootTask 'release/launcher/cavern-voxel-v1.png'))
$script:helperStateTask=@{Helper=$null};$script:targetTask=0;$script:pageTask=0;$script:imageCacheTask=@{};$script:itemsTask=[Collections.Generic.List[object]]::new()
function Get-NativeLabelTask($Stack) {
    if($null -eq $Stack -or $Stack.count -le 0){return '空槽'}
    $shortTask=$Stack.id.Split(':')[-1];$nameTask=$shortTask.Replace('_',' ')
    if($Stack.id -eq 'hollowcraft_bridge:zenith'){$nameTask='天顶剑'}
    elseif($Stack.id -eq 'hollowcraft_bridge:stardust_wings'){$nameTask='星尘翅膀'}
    elseif($script:translationsTask.ContainsKey('item.minecraft.'+$shortTask)){$nameTask=$script:translationsTask['item.minecraft.'+$shortTask]}
    elseif($script:translationsTask.ContainsKey('block.minecraft.'+$shortTask)){$nameTask=$script:translationsTask['block.minecraft.'+$shortTask]}
    if($Stack.nbt.Potion){$nameTask+=' · '+([string]$Stack.nbt.Potion).Split(':')[-1].Replace('_',' ')}
    $enchantsTask=if($Stack.nbt.StoredEnchantments){$Stack.nbt.StoredEnchantments}else{$Stack.nbt.Enchantments}
    if($enchantsTask){$nameTask+=' · '+(@($enchantsTask|ForEach-Object {([string]$_.id).Split(':')[-1]+' '+$_.lvl}) -join ', ')}
    return $nameTask
}
function Get-NativeImageTask([string]$Id) {
    if($script:imageCacheTask.ContainsKey($Id)){return $script:imageCacheTask[$Id]}
    $staticIconTask=$script:launcherIconsTask.Icon($Id);if($null -ne $staticIconTask){$script:imageCacheTask[$Id]=$staticIconTask;return $staticIconTask}
    $nameTask=$Id.Split(':')[-1];$pathTask=Join-Path $script:assetsTask ($nameTask+'.png')
    if($Id -like 'hollowcraft_bridge:*'){$pathTask=Join-Path $script:assetsTask ('terraria/'+$nameTask+'.png')}
    if($Id -like 'minecraft:shield*' -and -not(Test-Path -LiteralPath $pathTask)){$shieldPathTask=Join-Path $script:assetsTask 'shield_model.png';if(Test-Path -LiteralPath $shieldPathTask){$shieldTask=[Windows.Media.Imaging.BitmapImage]::new([Uri]$shieldPathTask);$shieldCropTask=[Windows.Media.Imaging.CroppedBitmap]::new($shieldTask,[Windows.Int32Rect]::new(1,1,12,22));$shieldCropTask.Freeze();$script:imageCacheTask[$Id]=$shieldCropTask;return $shieldCropTask}}
    if(-not(Test-Path -LiteralPath $pathTask)){return $null}
    $imageTask=[Windows.Media.Imaging.BitmapImage]::new();$imageTask.BeginInit();$imageTask.CacheOption='OnLoad';$imageTask.UriSource=[Uri]$pathTask;$imageTask.EndInit()
    if($imageTask.PixelHeight -gt $imageTask.PixelWidth*2){$imageTask=[Windows.Media.Imaging.CroppedBitmap]::new($imageTask,[Windows.Int32Rect]::new(0,0,$imageTask.PixelWidth,$imageTask.PixelWidth))}
    $imageTask.Freeze();$script:imageCacheTask[$Id]=$imageTask;return $imageTask
}
function Get-TargetStackTask([int]$Index) {
    if($Index -lt 36){return $script:settingsTask.loadout.slots[$Index]}
    if($Index -lt 40){return $script:settingsTask.loadout.armor[$Index-36]}
    return $script:settingsTask.loadout.offhand
}
function Set-TargetStackTask([int]$Index,$Stack) {
    if($Index -lt 36){$script:settingsTask.loadout.slots[$Index]=$Stack}
    elseif($Index -lt 40){$script:settingsTask.loadout.armor[$Index-36]=$Stack}
    else{$script:settingsTask.loadout.offhand=$Stack}
}
function Target-NameTask([int]$Index) {
    if($Index -lt 9){return '快捷栏 '+($Index+1)}
    if($Index -lt 36){return '背包 '+($Index-8)}
    if($Index -lt 40){return @('头盔','胸甲','护腿','靴子')[$Index-36]}
    return '副手'
}
function New-NativeItemButtonTask($Stack,[int]$Index,[bool]$Catalog=$false) {
    $buttonTask=[Windows.Controls.Button]::new();$buttonTask.Width=if($Catalog){54}else{48};$buttonTask.Height=if($Catalog){44}else{48};$buttonTask.Margin='2';$buttonTask.Padding='1'
    $buttonTask.Background=[Windows.Media.BrushConverter]::new().ConvertFromString('#223B46');$buttonTask.ToolTip=(Get-NativeLabelTask $Stack)+' '+$Stack.id
    $gridTask=[Windows.Controls.Grid]::new()
    if($Stack.count -gt 0 -and $Stack.id -ne 'minecraft:air'){
        $imageTask=[Windows.Controls.Image]::new();$imageTask.Source=Get-NativeImageTask $Stack.id;$imageTask.Width=30;$imageTask.Height=30;[Windows.Media.RenderOptions]::SetBitmapScalingMode($imageTask,'NearestNeighbor');$null=$gridTask.Children.Add($imageTask)
        if($null -eq $imageTask.Source){$fallbackTask=[Windows.Controls.TextBlock]::new();$fallbackTask.Text=($Stack.id.Split(':')[-1].Substring(0,[Math]::Min(3,$Stack.id.Split(':')[-1].Length)));$fallbackTask.FontSize=10;$fallbackTask.HorizontalAlignment='Center';$fallbackTask.VerticalAlignment='Center';$null=$gridTask.Children.Add($fallbackTask)}
        if($Stack.count -gt 1){$numberTask=[Windows.Controls.TextBlock]::new();$numberTask.Text=[string]$Stack.count;$numberTask.FontSize=11;$numberTask.HorizontalAlignment='Right';$numberTask.VerticalAlignment='Bottom';$numberTask.Margin='0,0,2,0';$null=$gridTask.Children.Add($numberTask)}
    }
    $buttonTask.Content=$gridTask
    if($Catalog){$buttonTask.Tag=$Stack;$buttonTask.Add_Click({param($senderTask,$eventTask)
        try{
            $pickedTask=$senderTask.Tag;$copyTask=$pickedTask|ConvertTo-Json -Depth 64|ConvertFrom-Json;$countTask=64;$null=[int]::TryParse($script:controlsTask.Quantity.Text,[ref]$countTask);$copyTask.count=[Math]::Max(1,[Math]::Min($countTask,[int]$script:itemIndexTask[$copyTask.id].maxCount))
            if($script:targetTask -ge 36 -and $script:targetTask -lt 40){$slotTask=@('head','chest','legs','feet')[$script:targetTask-36];if($script:itemIndexTask[$copyTask.id].equipment -ne $slotTask){throw ('该物品不能装备在'+(Target-NameTask $script:targetTask)+'栏')};$copyTask.count=1}
            Set-TargetStackTask $script:targetTask $copyTask;Refresh-NativeLoadoutTask;$script:controlsTask.Status.Text='已放入'+(Target-NameTask $script:targetTask)+'：'+(Get-NativeLabelTask $copyTask)
        }catch{$script:controlsTask.Status.Text=$_.Exception.Message}
    })}
    else{$buttonTask.Tag=$Index;if($Index -eq $script:targetTask){$buttonTask.BorderBrush=[Windows.Media.Brushes]::LightGoldenrodYellow}
        $buttonTask.Add_Click({param($senderTask,$eventTask)$script:targetTask=[int]$senderTask.Tag;$script:controlsTask.TargetHint.Text='目标：'+(Target-NameTask $script:targetTask);$script:controlsTask.MainTabs.SelectedItem=$script:controlsTask.CatalogTab;Refresh-NativeCatalogTask})
        $buttonTask.Add_PreviewMouseRightButtonDown({param($senderTask,$eventTask)Set-TargetStackTask ([int]$senderTask.Tag) (New-HollowCraftStackTask);Refresh-NativeLoadoutTask;$eventTask.Handled=$true})
    }
    return $buttonTask
}
function Refresh-NativeLoadoutTask {
    foreach($panelTask in @('HotbarSlots','BackpackSlots','ArmorSlots','OffhandSlot')){$script:controlsTask[$panelTask].Children.Clear()}
    for($iTask=0;$iTask -lt 36;$iTask++){$panelTask=if($iTask -lt 9){'HotbarSlots'}else{'BackpackSlots'};$null=$script:controlsTask[$panelTask].Children.Add((New-NativeItemButtonTask (Get-TargetStackTask $iTask) $iTask))}
    for($iTask=36;$iTask -lt 40;$iTask++){$buttonTask=New-NativeItemButtonTask (Get-TargetStackTask $iTask) $iTask;$buttonTask.Width=39;$null=$script:controlsTask.ArmorSlots.Children.Add($buttonTask)}
    $null=$script:controlsTask.OffhandSlot.Children.Add((New-NativeItemButtonTask (Get-TargetStackTask 40) 40))
}
$knownMobsTask=@((Get-Content -LiteralPath (Join-Path $nativeRootTask 'release/native-creature-models.json') -Raw -Encoding UTF8|ConvertFrom-Json).types|ForEach-Object {$_.id})
foreach($stackTask in @($registryTask.creativeStacks)+@((New-HollowCraftStackTask 'hollowcraft_bridge:zenith' 1),(New-HollowCraftStackTask 'hollowcraft_bridge:stardust_wings' 1))){
    if(-not $script:itemIndexTask.ContainsKey($stackTask.id)){continue}
    if($stackTask.id -like '*_spawn_egg' -and $script:itemIndexTask[$stackTask.id].entity -notin $knownMobsTask){continue}
    $stackTask=$stackTask|ConvertTo-Json -Depth 64|ConvertFrom-Json
    if(-not $stackTask.nbt){$stackTask|Add-Member nbt ([pscustomobject]@{}) -Force};$stackTask|Add-Member damage 0 -Force
    $script:itemsTask.Add([pscustomobject]@{Stack=$stackTask;Name=(Get-NativeLabelTask $stackTask);Search=((Get-NativeLabelTask $stackTask)+' '+$stackTask.id).ToLowerInvariant();Groups=[Collections.Generic.List[string]]::new()})
}
$groupNamesTask=@{building_blocks='建筑方块';colored_blocks='彩色方块';natural_blocks='自然方块';functional_blocks='功能方块';redstone_blocks='红石机关';tools='工具与实用';combat='战斗装备';food_and_drinks='食物与饮品';ingredients='原料';spawn_eggs='生物生成蛋';op_blocks='管理物品'}
$null=$script:controlsTask.Category.Items.Add([pscustomobject]@{Title='全部物品';Id='all'})
foreach($groupTask in $registryTask.creativeGroups){$shortTask=([string]$groupTask.id).Split(':')[-1];if(-not $groupNamesTask.ContainsKey($shortTask)){continue};$null=$script:controlsTask.Category.Items.Add([pscustomobject]@{Title=$groupNamesTask[$shortTask];Id=$groupTask.id});$idsTask=@($groupTask.entries|ForEach-Object {$registryTask.creativeStacks[[int]$_].id});foreach($itemTask in $script:itemsTask){if($itemTask.Stack.id -in $idsTask){$itemTask.Groups.Add([string]$groupTask.id)}}}
$script:controlsTask.Category.DisplayMemberPath='Title';$script:controlsTask.Category.SelectedIndex=0
function Refresh-NativeCatalogTask {
    $queryTask=$script:controlsTask.Search.Text.Trim().ToLowerInvariant();$categoryTask=$script:controlsTask.Category.SelectedItem.Id
    $filteredTask=@($script:itemsTask|Where-Object {(-not $queryTask -or $_.Search.Contains($queryTask)) -and ($categoryTask -eq 'all' -or $categoryTask -in $_.Groups)})
    $maximumTask=[Math]::Max(0,[int][Math]::Ceiling($filteredTask.Count/72.0)-1);$script:pageTask=[Math]::Max(0,[Math]::Min($script:pageTask,$maximumTask))
    $script:controlsTask.CatalogGrid.Children.Clear();foreach($entryTask in @($filteredTask|Select-Object -Skip ($script:pageTask*72) -First 72)){$null=$script:controlsTask.CatalogGrid.Children.Add((New-NativeItemButtonTask $entryTask.Stack -1 $true))}
    $script:controlsTask.PageHint.Text=($script:pageTask+1).ToString()+' / '+($maximumTask+1);$script:controlsTask.CatalogCount.Text=$filteredTask.Count.ToString()+' 项'
    $script:controlsTask.PrevPage.IsEnabled=$script:pageTask -gt 0;$script:controlsTask.NextPage.IsEnabled=$script:pageTask -lt $maximumTask
}
$script:controlsTask.KeepInventory.IsChecked=[bool]$script:settingsTask.keepInventory
$script:controlsTask.AutomaticWater.IsChecked=[bool]$script:settingsTask.automaticWater
$script:controlsTask.ApplyExisting.IsChecked=[bool]$script:settingsTask.applyLoadoutToExisting
foreach($delayTask in @(20,40,60,80,120)){$null=$script:controlsTask.WaterDelay.Items.Add([pscustomobject]@{Title=($delayTask/20).ToString()+' 秒';Ticks=$delayTask})}
$script:controlsTask.WaterDelay.DisplayMemberPath='Title';$script:controlsTask.WaterDelay.SelectedIndex=1
for($iTask=0;$iTask -lt $script:controlsTask.WaterDelay.Items.Count;$iTask++){if($script:controlsTask.WaterDelay.Items[$iTask].Ticks -eq $script:settingsTask.waterReturnTicks){$script:controlsTask.WaterDelay.SelectedIndex=$iTask}}
foreach($presetTask in @(@('classic','经典铁装 · 当前默认'),@('light','轻装开荒'),@('ranger','弓箭手'),@('netherite','下界勇士'),@('clamsk','Clamsk 彩蛋演示'),@('empty','空手生存'))){$null=$script:controlsTask.Preset.Items.Add([pscustomobject]@{Id=$presetTask[0];Title=$presetTask[1]})}
$script:controlsTask.Preset.DisplayMemberPath='Title';$script:controlsTask.Preset.SelectedIndex=0
$script:controlsTask.ApplyPreset.Add_Click({$script:settingsTask.loadout=New-HollowCraftLoadoutTask $script:controlsTask.Preset.SelectedItem.Id;Refresh-NativeLoadoutTask;$script:controlsTask.Status.Text='预设已载入，保存后生效。'})
$script:controlsTask.Search.Add_TextChanged({$script:pageTask=0;Refresh-NativeCatalogTask})
$script:controlsTask.Category.Add_SelectionChanged({$script:pageTask=0;Refresh-NativeCatalogTask})
$script:controlsTask.PrevPage.Add_Click({$script:pageTask--;Refresh-NativeCatalogTask})
$script:controlsTask.NextPage.Add_Click({$script:pageTask++;Refresh-NativeCatalogTask})
$script:controlsTask.BackToLoadout.Add_Click({$script:controlsTask.MainTabs.SelectedItem=$script:controlsTask.LoadoutTab})
foreach($bossTask in $catalogTask.items){$null=$script:controlsTask.BossChoice.Items.Add([pscustomobject]@{Title=$bossTask.zh+' / '+$bossTask.en;Id=$bossTask.id;Godhome=$bossTask.godhome})}
$script:controlsTask.BossChoice.DisplayMemberPath='Title';$script:controlsTask.BossChoice.SelectedIndex=0
foreach($levelTask in @('调谐 / Attuned','进升 / Ascended','辐辉 / Radiant')){$null=$script:controlsTask.BossLevel.Items.Add($levelTask)};$script:controlsTask.BossLevel.SelectedIndex=0
function Save-NativePreferencesTask {
    $script:settingsTask.keepInventory=[bool]$script:controlsTask.KeepInventory.IsChecked;$script:settingsTask.automaticWater=[bool]$script:controlsTask.AutomaticWater.IsChecked
    $script:settingsTask.waterReturnTicks=[int]$script:controlsTask.WaterDelay.SelectedItem.Ticks;$script:settingsTask.applyLoadoutToExisting=[bool]$script:controlsTask.ApplyExisting.IsChecked
    Save-HollowCraftSettingsTask $script:settingsPathTask $script:settingsTask $script:itemIndexTask
}
$script:controlsTask.SaveButton.Add_Click({try{Save-NativePreferencesTask;$script:controlsTask.Status.Text='配置已保存，下次启动生效。'}catch{$script:controlsTask.Status.Text=$_.Exception.Message}})
function Set-NativeBusyTask([bool]$Busy){foreach($nameTask in @('PlayButton','InstallButton','CloseButton','RecordButton','SaveButton')){$script:controlsTask[$nameTask].IsEnabled=-not $Busy}}
function Run-NativeActionTask([string]$Action,[string]$BossId='',[int]$Level=0) {
    if($null -ne $script:helperStateTask.Helper){return}
    try{
        if($Action -eq 'Play'){Save-NativePreferencesTask}
        Set-NativeBusyTask $true;$logRootTask=Join-Path $nativeRootTask 'runtime';New-Item -ItemType Directory -Path $logRootTask -Force|Out-Null
        $argsTask='-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "'+(Join-Path $PSScriptRoot 'native-launcher-run.ps1')+'" -Action '+$Action
        if($BossId){if($BossId -notmatch '^[a-zA-Z0-9_-]+$'){throw 'Invalid Boss id'};$argsTask+=' -BossId '+$BossId+' -BossLevel '+$Level+' -Width 1920 -Height 1080'}
        $helperTask=Start-Process -FilePath (Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe') -ArgumentList $argsTask -WindowStyle Hidden -RedirectStandardOutput (Join-Path $logRootTask 'native-launcher.stdout.log') -RedirectStandardError (Join-Path $logRootTask 'native-launcher.stderr.log') -PassThru
        $null=$helperTask.Handle;$script:helperStateTask.Helper=$helperTask;$script:controlsTask.Status.Text='正在处理…';$script:controlsTask.Details.Text=''
    }catch{Set-NativeBusyTask $false;$script:controlsTask.Status.Text='操作未完成';$script:controlsTask.Details.Text=$_.Exception.Message}
}
$script:controlsTask.PlayButton.Add_Click({Run-NativeActionTask 'Play'})
$script:controlsTask.InstallButton.Add_Click({Run-NativeActionTask 'Install'})
$script:controlsTask.CloseButton.Add_Click({Run-NativeActionTask 'Close'})
$script:controlsTask.RecordButton.Add_Click({$bossTask=$script:controlsTask.BossChoice.SelectedItem;if($bossTask){$levelTask=if($bossTask.Godhome){$script:controlsTask.BossLevel.SelectedIndex}else{0};Run-NativeActionTask 'Play' $bossTask.Id $levelTask}})
Refresh-NativeLoadoutTask;Refresh-NativeCatalogTask
if($PreviewPath){
    $script:controlsTask.MainTabs.SelectedIndex=$PreviewTab;$previewRootTask=$windowTask.Content;$previewRootTask.Resources=$windowTask.Resources;$windowTask.Content=$null;$previewRootTask.Measure([Windows.Size]::new(1120,788));$previewRootTask.Arrange([Windows.Rect]::new(0,0,1120,788));$previewRootTask.UpdateLayout()
    $bitmapTask=[Windows.Media.Imaging.RenderTargetBitmap]::new(1120,788,96,96,[Windows.Media.PixelFormats]::Pbgra32);$bitmapTask.Render($previewRootTask);$encoderTask=[Windows.Media.Imaging.PngBitmapEncoder]::new();$encoderTask.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmapTask));$outputTask=[IO.File]::Create($PreviewPath);try{$encoderTask.Save($outputTask)}finally{$outputTask.Dispose()};$windowTask.Close();Write-Output $PreviewPath;return
}
$timerTask=[Windows.Threading.DispatcherTimer]::new();$timerTask.Interval=[TimeSpan]::FromMilliseconds(500)
$timerTask.Add_Tick({try{$resultTask=Receive-HollowCraftNativeHelperTask $script:helperStateTask (Join-Path $nativeRootTask 'runtime/native-launcher.stdout.log') (Join-Path $nativeRootTask 'runtime/native-launcher.stderr.log');if($null -ne $resultTask){Set-NativeBusyTask $false;$script:controlsTask.Status.Text=if($resultTask.ExitCode -eq 0){'完成 / Ready'}else{'操作未完成'};$script:controlsTask.Details.Text=$resultTask.Message}}catch{Set-NativeBusyTask $false;$script:controlsTask.Details.Text=$_.Exception.Message}})
$timerTask.Start()
try{$null=$windowTask.ShowDialog()}finally{$timerTask.Stop();$helperTask=$script:helperStateTask.Helper;$script:helperStateTask.Helper=$null;if($null -ne $helperTask){$helperTask.Dispose()}}
