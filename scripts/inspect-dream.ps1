Add-Type -Path './HollowCraft/dependencies/hk-api/Mono.Cecil.dll'
$a=[Mono.Cecil.AssemblyDefinition]::ReadAssembly('C:/Program Files (x86)/Steam/steamapps/common/Hollow Knight/hollow_knight_Data/Managed/Assembly-CSharp.dll')
foreach($t in $a.MainModule.Types|Where-Object Name -Match 'Dreamnail|DreamNail|DreamGate|Dreamer') {Write-Output ('TYPE '+$t.FullName);$t.Methods | Where-Object Name -notmatch '^get_|^set_' | Select-Object Name}
($a.MainModule.Types|Where-Object Name -eq 'AttackTypes').Fields | Select-Object Name,Constant
$a.Dispose()
