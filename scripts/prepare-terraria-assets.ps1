param([string]$TerrariaRoot,[string]$AssetsRoot,[switch]$NativeOnly)
$ErrorActionPreference='Stop'
if($PSVersionTable.PSEdition -eq 'Core') {
    $argumentsTask=@('-NoProfile','-ExecutionPolicy','Bypass','-File',$PSCommandPath)
    if($TerrariaRoot){$argumentsTask+=@('-TerrariaRoot',$TerrariaRoot)}
    if($AssetsRoot){$argumentsTask+=@('-AssetsRoot',$AssetsRoot)}
    if($NativeOnly){$argumentsTask+='-NativeOnly'}
    & powershell.exe @argumentsTask
    if($LASTEXITCODE -ne 0){throw 'Terraria local import failed.'};return
}
$rootTask=Split-Path $PSScriptRoot -Parent
if(-not $AssetsRoot) {$AssetsRoot=Join-Path $rootTask 'artifacts\HollowCraft\HollowCraftAssets'}
if(-not $TerrariaRoot) {
    $steamTask=(Get-ItemProperty -LiteralPath 'HKCU:\Software\Valve\Steam' -Name SteamPath -ErrorAction SilentlyContinue).SteamPath
    if(-not $steamTask){$steamTask=Join-Path ${env:ProgramFiles(x86)} 'Steam'}
    $librariesTask=@($steamTask)
    $libraryFileTask=Join-Path $steamTask 'steamapps\libraryfolders.vdf'
    if(Test-Path -LiteralPath $libraryFileTask) {
        foreach($matchTask in [regex]::Matches([IO.File]::ReadAllText($libraryFileTask),'"path"\s+"([^"]+)"')) {$librariesTask+=$matchTask.Groups[1].Value.Replace('\\','\')}
    }
    foreach($libraryTask in $librariesTask) {
        if(-not(Test-Path -LiteralPath $libraryTask)){continue}
        $candidateTask=Join-Path $libraryTask 'steamapps\common\Terraria'
        if(Test-Path -LiteralPath (Join-Path $candidateTask 'Content\Images\Item_4956.xnb')) {$TerrariaRoot=$candidateTask;break}
    }
}
if(-not $TerrariaRoot) {Write-Output 'Terraria not installed; optional Clamsk assets skipped.';return}
$destinationTask=Join-Path $AssetsRoot 'terraria'
$packTask=Join-Path $rootTask 'runtime\minecraft\resourcepacks\HollowCraft-Terraria'
$texturesTask=Join-Path $packTask 'assets\hollowcraft_bridge\textures\item'
New-Item -ItemType Directory -Path $destinationTask -Force | Out-Null
if(-not $NativeOnly){New-Item -ItemType Directory -Path $texturesTask -Force | Out-Null}
if(-not ('TerrariaAssetImporter' -as [type])) {
    Add-Type -AssemblyName System.Drawing
    $sourceTask=@('LzxDecoder.cs','LzxDecoderStream.cs','Importer.cs') | ForEach-Object {Join-Path $rootTask ('tools\TerrariaAssetImporter\'+$_)}
    Add-Type -Path $sourceTask -ReferencedAssemblies System.Drawing
}
$itemsTask=@{zenith=4956;stardust_wings=3471;copper_shortsword=3507;starfury=65;terra_blade=757;enchanted_sword=989;bee_keeper=1123;horsemans_blade=1826;influx_waver=2880;seedler=3018;meowmere=3063;star_wrath=3065}
foreach($entryTask in $itemsTask.GetEnumerator()) {
    $targetTask=Join-Path $destinationTask ($entryTask.Key+'.png')
    [TerrariaAssetImporter]::Texture((Join-Path $TerrariaRoot ('Content\Images\Item_'+$entryTask.Value+'.xnb')),$targetTask)
    if(-not $NativeOnly -and $entryTask.Key -in @('zenith','stardust_wings')) {Copy-Item -LiteralPath $targetTask -Destination $texturesTask -Force}
}
$wingsTask=Join-Path $destinationTask 'stardust_sheet.png'
[TerrariaAssetImporter]::Texture((Join-Path $TerrariaRoot 'Content\Images\Wings_32.xnb'),$wingsTask)
if(-not $NativeOnly){[TerrariaAssetImporter]::WingFrames($wingsTask,$texturesTask,4)}
[TerrariaAssetImporter]::Sound((Join-Path $TerrariaRoot 'Content\Sounds\Item_169.xnb'),(Join-Path $destinationTask 'swing.wav'))
if($NativeOnly){[IO.File]::WriteAllText((Join-Path $destinationTask 'ready.txt'),'local Terraria import schema 1',[Text.UTF8Encoding]::new($false));Write-Output 'Imported original Terraria sprites and sword sound for Native HollowCraft.';return}
[IO.File]::WriteAllText((Join-Path $packTask 'pack.mcmeta'),'{"pack":{"pack_format":15,"description":"HollowCraft: locally imported Terraria assets"}}',[Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $destinationTask 'ready.txt'),'local Terraria import schema 1',[Text.UTF8Encoding]::new($false))
$optionsTask=Join-Path $rootTask 'runtime\minecraft\options.txt'
$linesTask=if(Test-Path -LiteralPath $optionsTask){[IO.File]::ReadAllLines($optionsTask)}else{@()}
$entryTask=$linesTask | Where-Object {$_ -like 'resourcePacks:*'} | Select-Object -First 1
Add-Type -AssemblyName System.Web.Extensions
$jsonTask=[Web.Script.Serialization.JavaScriptSerializer]::new()
$packsTask=[Collections.Generic.List[string]]::new()
$packsTask.Add('vanilla')
function Add-PackNamesTask($ValueTask) {
    if($ValueTask -is [string]) {
        $nameTask=$ValueTask.Trim();if($nameTask -and -not $packsTask.Contains($nameTask)){$packsTask.Add($nameTask)}
    } elseif($ValueTask -is [Collections.IDictionary]) {
        # Repair the PS5 ConvertTo-Json value/Count wrappers written by older imports.
        if($ValueTask.Keys -contains 'value'){Add-PackNamesTask ($ValueTask['value'])}
    } elseif($ValueTask -is [Collections.IEnumerable]) {
        foreach($partTask in $ValueTask){Add-PackNamesTask $partTask}
    }
}
if($entryTask){Add-PackNamesTask ($jsonTask.DeserializeObject($entryTask.Substring(14)))}
if(-not $packsTask.Contains('file/HollowCraft-Terraria')){$packsTask.Add('file/HollowCraft-Terraria')}
# Serialize concrete .NET strings, avoiding PS5's extended scalar properties.
$linesTask=@($linesTask | Where-Object {$_ -notlike 'resourcePacks:*'})+('resourcePacks:'+ $jsonTask.Serialize($packsTask))
[IO.File]::WriteAllLines($optionsTask,$linesTask,[Text.UTF8Encoding]::new($false))
Write-Output 'Imported original Terraria sprites and sword sound into the private HollowCraft profile.'
