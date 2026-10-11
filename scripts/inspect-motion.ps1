Add-Type -Path './HollowCraft/dependencies/hk-api/Mono.Cecil.dll'
$a=[Mono.Cecil.AssemblyDefinition]::ReadAssembly('C:/Program Files (x86)/Steam/steamapps/common/Hollow Knight/hollow_knight_Data/Managed/Assembly-CSharp.dll')
$t=$a.MainModule.Types | Where-Object Name -eq 'HeroController'
$t.Fields | Where-Object Name -Match 'JUMP|GRAVITY|SWIM' | Select-Object Name,FieldType
$t.Methods | Where-Object Name -Match 'Jump|Fall|Bench' | Select-Object Name
$a.Dispose()
