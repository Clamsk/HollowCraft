$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$evidence=Join-Path $taskRoot 'runtime/hk-smoke-saves'
$original=Get-FileHash -LiteralPath 'C:\Program Files (x86)\Steam\steamapps\common\Hollow Knight\hollow_knight_Data\Managed\Assembly-CSharp.dll' -Algorithm SHA256
$expected='E9048EF6A633970F735E01EC166D3959F610EAEA7A88D827D48D67B1E5FB87BD'
if($original.Hash -ne $expected) {throw 'Original Steam assembly hash changed'}
$tests=@()
foreach($name in @('baseline','adventure-complete','environment','containers','campaign-npc','false-knight','shop','choice')) {
    $file=Join-Path $evidence ($name+'-0.5-pass.json')
    $result=Get-Content -LiteralPath $file -Raw|ConvertFrom-Json
    if(-not $result.success) {throw ('Missing successful evidence: '+$name)}
    $tests += [ordered]@{name=$name;verifiedUtc=$result.utc;scene=$result.scene;description=$result.description;result=$file;log=Join-Path $evidence ($name+'-0.5-pass.log');fixture=if($name -eq 'false-knight') {'Actual native AI/phases; creative player fixture, not survival campaign completion'}else{'Isolated automated gameplay fixture'}}
}
$jar=Join-Path $taskRoot 'minecraft/build/libs/hollowcraft-minecraft-0.1.0.jar'
$zip=[IO.Compression.ZipFile]::OpenRead($jar)
try {$reader=[IO.StreamReader]::new($zip.GetEntry('fabric.mod.json').Open());try {$meta=$reader.ReadToEnd()|ConvertFrom-Json}finally{$reader.Dispose()}}finally{$zip.Dispose()}
$dll=Join-Path $taskRoot 'artifacts/HollowCraft/HollowCraft.dll'
$version=[Reflection.AssemblyName]::GetAssemblyName($dll).Version.ToString()
if($meta.version -ne '0.5.0' -or $version -ne '0.5.0.0') {throw 'Build metadata version mismatch'}
$builds=@($dll,$jar,(Join-Path $taskRoot 'artifacts/HollowCraft/Assembly-CSharp.dll'))|ForEach-Object {$hash=Get-FileHash -LiteralPath $_ -Algorithm SHA256;[ordered]@{path=$hash.Path;sha256=$hash.Hash;lastWriteUtc=(Get-Item -LiteralPath $_).LastWriteTimeUtc.ToString('o')}}
$report=[ordered]@{version='0.5.0';recordedUtc=(Get-Date).ToUniversalTime().ToString('o');sessionStartedUtc='2026-10-10T05:47:55Z';sessionDeadlineUtc='2026-10-10T12:47:55Z';minecraft='1.20.1';betterCombat='1.8.5';hollowKnight='1.5.12620 / Unity 6000.0.61f1';originalAssemblyUnchanged=$true;originalAssemblySha256=$original.Hash;builds=$builds;tests=$tests;transparentGuiIconCount=@(Get-ChildItem (Join-Path $taskRoot 'artifacts/HollowCraft/HollowCraftAssets/item_models_rgba') -Filter '*.png' -Recurse).Count;fullSurvivalCampaignVerified=$false;limits=@('Full survival route, dreams/endings and all bosses/map interactions remain unverified','Native terrain obstruction and aquatic movement use a 2D approximation','Complex piston/redstone machines and specialized recipe selector UIs are incomplete','Some builtin models, enchantment glint and full Better Combat conditional/dual-wield animation parity remain incomplete')}
$report|ConvertTo-Json -Depth 10|Set-Content -LiteralPath (Join-Path $taskRoot 'validation-0.5.json') -Encoding utf8
Write-Output ('Validated 0.5 metadata, original assembly hash and '+$tests.Count+' successful integration reports. Full survival campaign remains unverified.')
