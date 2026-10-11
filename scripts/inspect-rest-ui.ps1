Add-Type -Path './HollowCraft/dependencies/hk-api/Mono.Cecil.dll'
$a=[Mono.Cecil.AssemblyDefinition]::ReadAssembly('C:/Program Files (x86)/Steam/steamapps/common/Hollow Knight/hollow_knight_Data/Managed/Assembly-CSharp.dll')
foreach($name in @('DialogueBox','GameManager','HeroController')) {
 $t=$a.MainModule.Types|Where-Object Name -eq $name
 if($name -eq 'DialogueBox') {$t.Fields|Select-Object Name,FieldType,IsPublic}
 if($name -eq 'GameManager') {foreach($method in @('ReadyForRespawn','ChangeToScene')) {($t.Methods|Where-Object Name -eq $method).Body.Instructions|ForEach-Object ToString}}
 if($name -eq 'HeroController') {($t.Methods|Where-Object Name -eq 'SetBenchRespawn').Body.Instructions|ForEach-Object ToString}
}
$a.Dispose()
