$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$original=Get-FileHash -LiteralPath 'C:\Program Files (x86)\Steam\steamapps\common\Hollow Knight\hollow_knight_Data\Managed\Assembly-CSharp.dll' -Algorithm SHA256
if($original.Hash -ne 'E9048EF6A633970F735E01EC166D3959F610EAEA7A88D827D48D67B1E5FB87BD'){throw 'Original Steam assembly changed'}
$builds=@('artifacts/HollowCraft/HollowCraft.dll','artifacts/HollowCraft/Assembly-CSharp.dll','minecraft/build/libs/hollowcraft-minecraft-0.1.0.jar')|ForEach-Object {$file=Join-Path $taskRoot $_;$hash=Get-FileHash -LiteralPath $file;[ordered]@{path=$file;sha256=$hash.Hash;lastWriteUtc=(Get-Item -LiteralPath $file).LastWriteTimeUtc.ToString('o')}}
$latestBuild=($builds|ForEach-Object {[datetime]$_.lastWriteUtc}|Sort-Object -Descending|Select-Object -First 1)
$tests=@('controls','baseline','environment','adventure')|ForEach-Object {
    $file=Join-Path $taskRoot ('runtime/hk-smoke-saves/'+$_+'-hotfix-pass.json')
    $result=Get-Content -LiteralPath $file -Raw|ConvertFrom-Json
    if(-not $result.success -or [datetime]$result.utc -lt $latestBuild){throw ('Missing current-build passing result: '+$_)}
    [ordered]@{name=$_;verifiedUtc=$result.utc;description=$result.description;result=$file;log=Join-Path $taskRoot ('runtime/hk-smoke-saves/'+$_+'-hotfix-pass.log')}
}
$report=[ordered]@{version='0.5.0 controls hotfix';recordedUtc=(Get-Date).ToUniversalTime().ToString('o');originalAssemblyUnchanged=$true;builds=$builds;tests=$tests;fullSurvivalCampaignVerified=$false;terrainLimit='Native platforms use approximate XY voxel shapes; ground edges up to .08 blocks are treated as a shared grid boundary';animationLimit='Vanilla locomotion and Better Combat attack clips; complete conditional/dual-wield parity and some built-in item models remain incomplete'}
$report|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $taskRoot 'validation-controls-hotfix.json') -Encoding utf8
Write-Output 'Validated current-build controls, baseline, environment and adventure integration evidence; Steam original unchanged.'
