Add-Type -Path './HollowCraft/dependencies/hk-api/Mono.Cecil.dll'
$a=[Mono.Cecil.AssemblyDefinition]::ReadAssembly('C:/Program Files (x86)/Steam/steamapps/common/Hollow Knight/hollow_knight_Data/Managed/Assembly-CSharp.dll')
foreach($name in @('HeroController','GameManager','DialogueBox')) {
 $t=$a.MainModule.Types|Where-Object Name -eq $name
 Write-Output ('TYPE '+$name)
 $t.Methods|Where-Object Name -Match 'BenchRespawn|Respawn|Death|SceneTransition|ChangeToScene|Conversation|ShowNext|Dialogue'|Select-Object FullName
}
$a.Dispose()
