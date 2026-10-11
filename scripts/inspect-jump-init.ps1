Add-Type -Path './HollowCraft/dependencies/hk-api/Mono.Cecil.dll'
$a=[Mono.Cecil.AssemblyDefinition]::ReadAssembly('C:/Program Files (x86)/Steam/steamapps/common/Hollow Knight/hollow_knight_Data/Managed/Assembly-CSharp.dll')
$t=$a.MainModule.Types|Where-Object Name -eq 'HeroController';foreach($name in @('HeroJump','HeroJumpNoEffect','CancelJump')) {$m=$t.Methods|Where-Object Name -eq $name;Write-Output $m.FullName;$m.Body.Instructions|ForEach-Object ToString}
$a.Dispose()
