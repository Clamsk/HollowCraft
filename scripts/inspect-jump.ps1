Add-Type -Path './HollowCraft/dependencies/hk-api/Mono.Cecil.dll'
$a=[Mono.Cecil.AssemblyDefinition]::ReadAssembly('C:/Program Files (x86)/Steam/steamapps/common/Hollow Knight/hollow_knight_Data/Managed/Assembly-CSharp.dll')
$t=$a.MainModule.Types|Where-Object Name -eq 'HeroController';($t.Methods|Where-Object Name -eq 'Jump').Body.Instructions|ForEach-Object ToString
$a.Dispose()
