param([string]$MinecraftJar='C:\Program Files (x86)\MC\.minecraft\versions\1.20.1\1.20.1.jar')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskModels=Get-Content -LiteralPath (Join-Path $taskRoot 'artifacts/native-creature-export/models.json') -Raw | ConvertFrom-Json
$taskRegistry=Get-Content -LiteralPath (Join-Path $taskRoot 'release/native-registry.json') -Raw | ConvertFrom-Json
$taskTextures=@{
    allay='allay/allay';axolotl='axolotl/axolotl_blue';bat='bat';bee='bee/bee';blaze='blaze';cat='cat/tabby';camel='camel/camel';cave_spider='spider/cave_spider';chicken='chicken';cod='fish/cod';cow='cow/cow';creeper='creeper/creeper';dolphin='dolphin';donkey='horse/donkey';drowned='zombie/drowned';elder_guardian='guardian_elder';enderman='enderman/enderman';endermite='endermite';evoker='illager/evoker';fox='fox/fox';frog='frog/temperate_frog';ghast='ghast/ghast';glow_squid='squid/glow_squid';goat='goat/goat';guardian='guardian';hoglin='hoglin/hoglin';horse='horse/horse_brown';husk='zombie/husk';illusioner='illager/illusioner';iron_golem='iron_golem/iron_golem';llama='llama/creamy';magma_cube='slime/magmacube';mooshroom='cow/red_mooshroom';mule='horse/mule';ocelot='cat/ocelot';panda='panda/panda';parrot='parrot/parrot_red_blue';phantom='phantom';pig='pig/pig';piglin='piglin/piglin';piglin_brute='piglin/piglin_brute';pillager='illager/pillager';polar_bear='bear/polarbear';pufferfish='fish/pufferfish';rabbit='rabbit/brown';ravager='illager/ravager';salmon='fish/salmon';sheep='sheep/sheep';shulker='shulker/shulker';silverfish='silverfish';skeleton='skeleton/skeleton';skeleton_horse='horse/horse_skeleton';slime='slime/slime';sniffer='sniffer/sniffer';snow_golem='snow_golem';spider='spider/spider';squid='squid/squid';stray='skeleton/stray';strider='strider/strider';tadpole='tadpole/tadpole';trader_llama='llama/trader_llama';tropical_fish='fish/tropical_a';turtle='turtle/big_sea_turtle';vex='illager/vex';villager='villager/villager';vindicator='illager/vindicator';wandering_trader='wandering_trader';warden='warden/warden';witch='witch';wither_skeleton='skeleton/wither_skeleton';wolf='wolf/wolf';zoglin='hoglin/zoglin';zombie='zombie/zombie';zombie_horse='horse/horse_zombie';zombie_villager='zombie_villager/zombie_villager';zombified_piglin='piglin/zombified_piglin'
}
$taskAliases=@{mooshroom='cow';glow_squid='squid';zombified_piglin='piglin';trader_llama='llama';pufferfish='pufferfish_big';tropical_fish='tropical_fish_small'}
$taskHostile=@('blaze','cave_spider','creeper','drowned','elder_guardian','endermite','evoker','ghast','guardian','hoglin','husk','illusioner','magma_cube','phantom','piglin_brute','pillager','ravager','shulker','silverfish','skeleton','slime','spider','stray','vex','vindicator','warden','witch','wither_skeleton','zoglin','zombie','zombie_villager')
$taskNeutral=@('bee','enderman','iron_golem','llama','piglin','polar_bear','wolf','zombified_piglin')
$taskFly=@('allay','bat','bee','blaze','ghast','parrot','phantom','vex')
$taskSwim=@('axolotl','cod','dolphin','elder_guardian','glow_squid','guardian','pufferfish','salmon','squid','tadpole','tropical_fish')
$taskHop=@('frog','magma_cube','rabbit','slime')
$taskRanged=@{skeleton='arrow';stray='arrow';pillager='arrow';ghast='fireball';blaze='small_fireball';witch='potion';evoker='magic';illusioner='arrow';guardian='beam';elder_guardian='beam';shulker='bullet'}
$taskTypes=[Collections.Generic.List[object]]::new()
$taskTextures.trader_llama='llama/creamy'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskZip=[IO.Compression.ZipFile]::OpenRead($MinecraftJar)
try{foreach($taskName in ($taskTextures.Keys | Sort-Object)){
    $taskId='minecraft:'+$taskName;$taskModelName=$taskName
    if($taskId -notin $taskRegistry.items.entity){continue}
    if($taskAliases.ContainsKey($taskName)){$taskModelName=$taskAliases[$taskName]}
    $taskModel=$taskModels.models.PSObject.Properties['minecraft:'+$taskModelName+'#main']
    $taskEntity=$taskRegistry.entities | Where-Object id -EQ $taskId | Select-Object -First 1
    $taskSource='assets/minecraft/textures/entity/'+$taskTextures[$taskName]+'.png'
    if(-not $taskModel -or -not $taskEntity -or -not $taskZip.GetEntry($taskSource)){Write-Warning ('Unresolved model/texture: '+$taskId);continue}
    $taskMode=if($taskFly -contains $taskName){'fly'}elseif($taskSwim -contains $taskName){'swim'}elseif($taskHop -contains $taskName){'hop'}else{'walk'}
    $taskDisposition=if($taskHostile -contains $taskName){'hostile'}elseif($taskNeutral -contains $taskName){'neutral'}else{'passive'}
    $taskValue=@{id=$taskId;texture='mob_v6_'+$taskName;sourceTexture=$taskSource;motion=$taskMode;disposition=$taskDisposition;ranged=$taskRanged[$taskName];model=$taskModel.Value}
    $taskLayers=[Collections.Generic.List[object]]::new()
    if($taskName -eq 'sheep'){$taskExtra=$taskModels.models.PSObject.Properties['minecraft:sheep#fur'];if($taskExtra){$taskLayers.Add(@{texture='mob_v6_sheep_fur';sourceTexture='assets/minecraft/textures/entity/sheep/sheep_fur.png';model=$taskExtra.Value})}}
    if($taskName -eq 'slime'){$taskExtra=$taskModels.models.PSObject.Properties['minecraft:slime#outer'];if($taskExtra){$taskLayers.Add(@{texture=$taskValue.texture;sourceTexture=$taskSource;model=$taskExtra.Value})}}
    if($taskName -eq 'trader_llama'){$taskLayers.Add(@{texture='mob_v6_trader_llama_decor';sourceTexture='assets/minecraft/textures/entity/llama/decor/trader_llama.png';model=$taskModel.Value})}
    $taskEyes=if($taskName -eq 'enderman'){'enderman/enderman_eyes'}elseif($taskName -eq 'spider' -or $taskName -eq 'cave_spider'){'spider_eyes'}elseif($taskName -eq 'phantom'){'phantom_eyes'}else{$null}
    if($taskEyes -and $taskZip.GetEntry('assets/minecraft/textures/entity/'+$taskEyes+'.png')){$taskLayers.Add(@{texture=('mob_v6_'+$taskName+'_eyes');sourceTexture=('assets/minecraft/textures/entity/'+$taskEyes+'.png');model=$taskModel.Value})}
    $taskValue.layers=$taskLayers.ToArray();$taskTypes.Add($taskValue)
}}finally{$taskZip.Dispose()}
$taskBoats=[Collections.Generic.List[object]]::new()
foreach($taskWood in @('oak','spruce','birch','jungle','acacia','cherry','dark_oak','mangrove','bamboo')){foreach($taskChest in @($false,$true)){$taskFolder=if($taskChest){'chest_boat'}else{'boat'};$taskSuffix=if($taskWood -eq 'bamboo'){if($taskChest){'chest_raft'}else{'raft'}}else{if($taskChest){'chest_boat'}else{'boat'}};$taskBoatModel=$taskModels.models.PSObject.Properties['minecraft:'+$taskFolder+'/'+$taskWood+'#main'];$taskBoats.Add(@{id=('minecraft:'+$taskWood+'_'+$taskSuffix);texture=('mob_v7_'+$taskFolder+'_'+$taskWood);sourceTexture=('assets/minecraft/textures/entity/'+$taskFolder+'/'+$taskWood+'.png');model=$taskBoatModel.Value;motion='boat';disposition='passive';layers=@()})}}
@{schema=1;minecraft='1.20.1';source='Build-time model part/pivot/UV descriptions; game code and texture pixels excluded';types=$taskTypes.ToArray();boats=$taskBoats.ToArray()} | ConvertTo-Json -Depth 64 -Compress | Set-Content -LiteralPath (Join-Path $taskRoot 'release/native-creature-models.json') -Encoding UTF8
Write-Output ('Native creature catalog prepared: '+$taskTypes.Count+' species.')
