param([string]$GameRoot,[switch]$UsePrebuilt)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'hollowcraft-common.ps1')
$configTask=Get-HollowCraftConfigTask
if(-not $GameRoot) {$GameRoot=$configTask.gameRoot}
$projectTaskRoot=Split-Path $PSScriptRoot -Parent
$gameManagedTask=Join-Path $GameRoot 'hollow_knight_Data\Managed'
$outTask=Join-Path $projectTaskRoot 'artifacts\HollowCraft'
New-Item -ItemType Directory -Path $outTask -Force | Out-Null
if(-not $UsePrebuilt) {
    & dotnet build (Join-Path $projectTaskRoot 'hollow-knight\HollowCraft.csproj') -c Release --verbosity quiet "-p:GameManaged=$gameManagedTask"
    if ($LASTEXITCODE -ne 0) { throw 'Hollow Knight bridge build failed' }
    Copy-Item -LiteralPath (Join-Path $projectTaskRoot 'hollow-knight\bin\Release\net472\HollowCraft.dll') -Destination $outTask -Force
} elseif(-not(Test-Path -LiteralPath (Join-Path $outTask 'HollowCraft.dll'))) {throw 'Prebuilt HollowCraft.dll is missing. Download the Windows release ZIP.'}
Add-Type -Path (Join-Path $projectTaskRoot 'dependencies\hk-api\Mono.Cecil.dll')
$resolverTask=[Mono.Cecil.DefaultAssemblyResolver]::new()
$resolverTask.AddSearchDirectory($gameManagedTask)
$resolverTask.AddSearchDirectory($outTask)
$readerTask=[Mono.Cecil.ReaderParameters]::new()
$readerTask.AssemblyResolver=$resolverTask
$gameAssemblyTask=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $gameManagedTask 'Assembly-CSharp.dll'),$readerTask)
try {
    $moduleTask=$gameAssemblyTask.MainModule
    $versionTask=($moduleTask.Types | Where-Object Name -EQ 'Constants').Fields | Where-Object Name -EQ 'GAME_VERSION'
    if ($versionTask.Constant -ne '1.5.12620') { throw "Unsupported game version: $($versionTask.Constant). Expected 1.5.12620." }
    if ($moduleTask.AssemblyReferences.Name -contains 'HollowCraft') { throw 'Build against the original Assembly-CSharp.dll, not an already patched file.' }
    $startTask=$moduleTask.Types | Where-Object Name -EQ 'StartManager'
    $awakeTask=$startTask.Methods | Where-Object Name -EQ 'Awake'
    if (-not $awakeTask.HasBody) { throw 'Expected StartManager.Awake was not found; patch aborted.' }
    $bridgeVersionTask=[Reflection.AssemblyName]::GetAssemblyName((Join-Path $outTask 'HollowCraft.dll')).Version
    $assemblyRefTask=[Mono.Cecil.AssemblyNameReference]::new('HollowCraft',$bridgeVersionTask)
    $moduleTask.AssemblyReferences.Add($assemblyRefTask)
    $campaignTypeTask=[Mono.Cecil.TypeReference]::new('HollowCraft','CampaignSession',$moduleTask,$assemblyRefTask)
    $newGameTask=($moduleTask.Types | Where-Object Name -eq 'GameManager').Methods | Where-Object Name -eq 'StartNewGame'
    $newGameHookTask=[Mono.Cecil.MethodReference]::new('NewGame',$moduleTask.TypeSystem.Void,$campaignTypeTask)
    $newGameHookTask.Parameters.Add([Mono.Cecil.ParameterDefinition]::new($newGameTask.DeclaringType))
    $newGameIlTask=$newGameTask.Body.GetILProcessor();$newGameFirstTask=$newGameTask.Body.Instructions[0]
    $newGameIlTask.InsertBefore($newGameFirstTask,$newGameIlTask.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0))
    $newGameIlTask.InsertBefore($newGameFirstTask,$newGameIlTask.Create([Mono.Cecil.Cil.OpCodes]::Call,$newGameHookTask))
    $typeRefTask=[Mono.Cecil.TypeReference]::new('HollowCraft','HollowCraftBootstrap',$moduleTask,$assemblyRefTask)
    $methodRefTask=[Mono.Cecil.MethodReference]::new('Initialize',$moduleTask.TypeSystem.Void,$typeRefTask)
    $methodRefTask.HasThis=$false
    $ilTask=$awakeTask.Body.GetILProcessor()
    $instructionTask=$ilTask.Create([Mono.Cecil.Cil.OpCodes]::Call,$methodRefTask)
    $ilTask.InsertBefore($awakeTask.Body.Instructions[0],$instructionTask)
    $controlsTypeTask=[Mono.Cecil.TypeReference]::new('HollowCraft','MinecraftControls',$moduleTask,$assemblyRefTask)
    $moveHookTask=[Mono.Cecil.MethodReference]::new('AdjustMove',$moduleTask.TypeSystem.Single,$controlsTypeTask)
    $moveHookTask.Parameters.Add([Mono.Cecil.ParameterDefinition]::new($moduleTask.TypeSystem.Single))
    $heroTypeTask=$moduleTask.Types | Where-Object Name -eq 'HeroController'
    $battleTypeTask=[Mono.Cecil.TypeReference]::new('HollowCraft','NativeBattle',$moduleTask,$assemblyRefTask)
    $healthTypeTask=$moduleTask.Types | Where-Object Name -eq 'HealthManager'
    foreach($nativeMethodTask in @('Die','SendDeathEvent','SetBattleScene','Awake')) {
        $nativeTask=$healthTypeTask.Methods | Where-Object Name -eq $nativeMethodTask | Select-Object -First 1
        if(-not $nativeTask.HasBody) {throw "HealthManager.$nativeMethodTask missing; abort patch"}
        $hookNameTask=if($nativeMethodTask -eq 'SetBattleScene') {'Bound'}elseif($nativeMethodTask -eq 'Awake') {'Track'}else{'Death'}
        $hookTask=[Mono.Cecil.MethodReference]::new($hookNameTask,$moduleTask.TypeSystem.Void,$battleTypeTask)
        $hookTask.Parameters.Add([Mono.Cecil.ParameterDefinition]::new($healthTypeTask))
        $nativeIlTask=$nativeTask.Body.GetILProcessor()
        # Replace each ret in place so branches to that ret also execute the hook.
        foreach($returnTask in @($nativeTask.Body.Instructions | Where-Object {$_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ret})) {
            $returnTask.OpCode=[Mono.Cecil.Cil.OpCodes]::Ldarg_0;$returnTask.Operand=$null
            $callTask=$nativeIlTask.Create([Mono.Cecil.Cil.OpCodes]::Call,$hookTask)
            $nativeIlTask.InsertAfter($returnTask,$callTask)
            $nativeIlTask.InsertAfter($callTask,$nativeIlTask.Create([Mono.Cecil.Cil.OpCodes]::Ret))
        }
        foreach($branchTask in $nativeTask.Body.Instructions | Where-Object {$_.OpCode.OperandType -eq [Mono.Cecil.Cil.OperandType]::ShortInlineBrTarget}) {
            $longNameTask=$branchTask.OpCode.Code.ToString() -replace '_S$',''
            $branchTask.OpCode=[Mono.Cecil.Cil.OpCodes].GetField($longNameTask).GetValue($null)
        }
    }
    $transitionTypeTask=[Mono.Cecil.TypeReference]::new('HollowCraft','SceneTransitionBridge',$moduleTask,$assemblyRefTask)
    $beginTransitionHookTask=[Mono.Cecil.MethodReference]::new('Begin',$moduleTask.TypeSystem.Void,$transitionTypeTask)
    $enterTransitionHookTask=[Mono.Cecil.MethodReference]::new('Enter',$moduleTask.TypeSystem.Void,$transitionTypeTask)
    $enterTransitionHookTask.Parameters.Add([Mono.Cecil.ParameterDefinition]::new($heroTypeTask))
    $arrivedHookTask=[Mono.Cecil.MethodReference]::new('Arrived',$moduleTask.TypeSystem.Void,$transitionTypeTask)
    $arrivedHookTask.Parameters.Add([Mono.Cecil.ParameterDefinition]::new($heroTypeTask))
    # Restore native physics before HK captures gravity and starts gate movement.
    $beginSceneTask=($moduleTask.Types | Where-Object Name -eq 'GameManager').Methods | Where-Object Name -eq 'BeginSceneTransition' | Select-Object -First 1
    if(-not $beginSceneTask.HasBody) {throw 'GameManager.BeginSceneTransition missing; abort patch'}
    $beginSceneIlTask=$beginSceneTask.Body.GetILProcessor()
    $beginSceneIlTask.InsertBefore($beginSceneTask.Body.Instructions[0],$beginSceneIlTask.Create([Mono.Cecil.Cil.OpCodes]::Call,$beginTransitionHookTask))
    foreach($entryNameTask in @('LeaveScene','EnterScene','EnterSceneDreamGate')) {
        $entryMethodTask=$heroTypeTask.Methods | Where-Object Name -eq $entryNameTask | Select-Object -First 1
        if(-not $entryMethodTask.HasBody) {throw "HeroController.$entryNameTask missing; abort patch"}
        $entryIlTask=$entryMethodTask.Body.GetILProcessor();$entryFirstTask=$entryMethodTask.Body.Instructions[0]
        $entryIlTask.InsertBefore($entryFirstTask,$entryIlTask.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0))
        $entryIlTask.InsertBefore($entryFirstTask,$entryIlTask.Create([Mono.Cecil.Cil.OpCodes]::Call,$enterTransitionHookTask))
    }
    $arrivedTask=$heroTypeTask.Methods | Where-Object Name -eq 'FinishedEnteringScene' | Select-Object -First 1
    if(-not $arrivedTask.HasBody) {throw 'HeroController.FinishedEnteringScene missing; abort patch'}
    $arrivedIlTask=$arrivedTask.Body.GetILProcessor()
    # Modify ret in place so branches to the old return also execute the hook.
    foreach($returnTask in @($arrivedTask.Body.Instructions | Where-Object OpCode -eq ([Mono.Cecil.Cil.OpCodes]::Ret))) {
        $returnTask.OpCode=[Mono.Cecil.Cil.OpCodes]::Ldarg_0;$returnTask.Operand=$null
        $callArrivalTask=$arrivedIlTask.Create([Mono.Cecil.Cil.OpCodes]::Call,$arrivedHookTask)
        $arrivedIlTask.InsertAfter($returnTask,$callArrivalTask)
        $arrivedIlTask.InsertAfter($callArrivalTask,$arrivedIlTask.Create([Mono.Cecil.Cil.OpCodes]::Ret))
    }
    $moveTask=$heroTypeTask.Methods | Where-Object { $_.Name -eq 'Move' -and $_.Parameters.Count -eq 1 }
    if(-not $moveTask.HasBody) { throw 'HeroController.Move missing; abort patch' }
    $moveIlTask=$moveTask.Body.GetILProcessor()
    $moveFirstTask=$moveTask.Body.Instructions[0]
    $moveIlTask.InsertBefore($moveFirstTask,$moveIlTask.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_1))
    $moveIlTask.InsertBefore($moveFirstTask,$moveIlTask.Create([Mono.Cecil.Cil.OpCodes]::Call,$moveHookTask))
    $moveIlTask.InsertBefore($moveFirstTask,$moveIlTask.Create([Mono.Cecil.Cil.OpCodes]::Starg,$moveTask.Parameters[0]))
    $cursorTypeTask=[Mono.Cecil.TypeReference]::new('HollowCraft','GameCursor',$moduleTask,$assemblyRefTask)
    $cursorHookTask=[Mono.Cecil.MethodReference]::new('ApplyCursor',$moduleTask.TypeSystem.Boolean,$cursorTypeTask)
    $inputTypeTask=$moduleTask.Types | Where-Object Name -eq 'InputHandler'
    $cursorMethodTask=$inputTypeTask.Methods | Where-Object Name -eq 'SetCursorEnabled'
    $cursorIlTask=$cursorMethodTask.Body.GetILProcessor();$cursorFirstTask=$cursorMethodTask.Body.Instructions[0]
    $cursorIlTask.InsertBefore($cursorFirstTask,$cursorIlTask.Create([Mono.Cecil.Cil.OpCodes]::Call,$cursorHookTask))
    $cursorIlTask.InsertBefore($cursorFirstTask,$cursorIlTask.Create([Mono.Cecil.Cil.OpCodes]::Brfalse,$cursorFirstTask))
    $cursorIlTask.InsertBefore($cursorFirstTask,$cursorIlTask.Create([Mono.Cecil.Cil.OpCodes]::Ret))
    $playerTypeTask=[Mono.Cecil.TypeReference]::new('HollowCraft','PlayerSystem',$moduleTask,$assemblyRefTask)
    $landingHookTask=[Mono.Cecil.MethodReference]::new('SuppressKnightLanding',$moduleTask.TypeSystem.Boolean,$playerTypeTask)
    $landingTask=$heroTypeTask.Methods | Where-Object Name -eq 'ShouldHardLand'
    $landingIlTask=$landingTask.Body.GetILProcessor();$landingFirstTask=$landingTask.Body.Instructions[0]
    $landingIlTask.InsertBefore($landingFirstTask,$landingIlTask.Create([Mono.Cecil.Cil.OpCodes]::Call,$landingHookTask))
    $landingIlTask.InsertBefore($landingFirstTask,$landingIlTask.Create([Mono.Cecil.Cil.OpCodes]::Brfalse,$landingFirstTask))
    $landingIlTask.InsertBefore($landingFirstTask,$landingIlTask.Create([Mono.Cecil.Cil.OpCodes]::Ldc_I4_0))
    $landingIlTask.InsertBefore($landingFirstTask,$landingIlTask.Create([Mono.Cecil.Cil.OpCodes]::Ret))
    $damageTask=$heroTypeTask.Methods | Where-Object { $_.Name -eq 'TakeDamage' -and $_.Parameters.Count -eq 4 }
    $damageHookTask=[Mono.Cecil.MethodReference]::new('RouteDamage',$moduleTask.TypeSystem.Boolean,$playerTypeTask)
    foreach($parameterTask in $damageTask.Parameters) {$damageHookTask.Parameters.Add([Mono.Cecil.ParameterDefinition]::new($parameterTask.ParameterType))}
    $damageIlTask=$damageTask.Body.GetILProcessor();$damageFirstTask=$damageTask.Body.Instructions[0]
    foreach($parameterTask in $damageTask.Parameters) {$damageIlTask.InsertBefore($damageFirstTask,$damageIlTask.Create([Mono.Cecil.Cil.OpCodes]::Ldarg,$parameterTask))}
    $damageIlTask.InsertBefore($damageFirstTask,$damageIlTask.Create([Mono.Cecil.Cil.OpCodes]::Call,$damageHookTask))
    $damageIlTask.InsertBefore($damageFirstTask,$damageIlTask.Create([Mono.Cecil.Cil.OpCodes]::Brfalse,$damageFirstTask))
    $damageIlTask.InsertBefore($damageFirstTask,$damageIlTask.Create([Mono.Cecil.Cil.OpCodes]::Ret))
    $resourceHookTask=[Mono.Cecil.MethodReference]::new('SuppressKnightResources',$moduleTask.TypeSystem.Boolean,$playerTypeTask)
    $benchHookTask=[Mono.Cecil.MethodReference]::new('BenchCheckpoint',$moduleTask.TypeSystem.Void,$playerTypeTask)
    $benchTask=$heroTypeTask.Methods | Where-Object Name -EQ 'SetBenchRespawn' | Select-Object -First 1
    $benchIlTask=$benchTask.Body.GetILProcessor()
    foreach($returnTask in @($benchTask.Body.Instructions | Where-Object { $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ret })) {$benchIlTask.InsertBefore($returnTask,$benchIlTask.Create([Mono.Cecil.Cil.OpCodes]::Call,$benchHookTask))}
    $jumpHookTask=[Mono.Cecil.MethodReference]::new('MinecraftJump',$moduleTask.TypeSystem.Boolean,$playerTypeTask)
    $jumpHookTask.Parameters.Add([Mono.Cecil.ParameterDefinition]::new($heroTypeTask))
    foreach($jumpNameTask in @('HeroJump','HeroJumpNoEffect')) {
        $jumpTask=$heroTypeTask.Methods | Where-Object Name -EQ $jumpNameTask | Select-Object -First 1
        $jumpIlTask=$jumpTask.Body.GetILProcessor();$jumpFirstTask=$jumpTask.Body.Instructions[0]
        $jumpIlTask.InsertBefore($jumpFirstTask,$jumpIlTask.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0))
        $jumpIlTask.InsertBefore($jumpFirstTask,$jumpIlTask.Create([Mono.Cecil.Cil.OpCodes]::Call,$jumpHookTask))
        $jumpIlTask.InsertBefore($jumpFirstTask,$jumpIlTask.Create([Mono.Cecil.Cil.OpCodes]::Brfalse,$jumpFirstTask))
        $jumpIlTask.InsertBefore($jumpFirstTask,$jumpIlTask.Create([Mono.Cecil.Cil.OpCodes]::Ret))
    }
    # Minecraft jumps have a fixed impulse. The Knight's release handler otherwise
    # zeros Y velocity even though HeroJump was replaced, truncating short presses.
    $releaseHookTask=[Mono.Cecil.MethodReference]::new('SuppressKnightJumpRelease',$moduleTask.TypeSystem.Boolean,$playerTypeTask)
    $releaseTask=$heroTypeTask.Methods | Where-Object Name -EQ 'JumpReleased' | Select-Object -First 1
    if(-not $releaseTask.HasBody) {throw 'HeroController.JumpReleased missing; abort patch'}
    $releaseIlTask=$releaseTask.Body.GetILProcessor();$releaseFirstTask=$releaseTask.Body.Instructions[0]
    $releaseIlTask.InsertBefore($releaseFirstTask,$releaseIlTask.Create([Mono.Cecil.Cil.OpCodes]::Call,$releaseHookTask))
    $releaseIlTask.InsertBefore($releaseFirstTask,$releaseIlTask.Create([Mono.Cecil.Cil.OpCodes]::Brfalse,$releaseFirstTask))
    $releaseIlTask.InsertBefore($releaseFirstTask,$releaseIlTask.Create([Mono.Cecil.Cil.OpCodes]::Ret))
    $abilityHookTask=[Mono.Cecil.MethodReference]::new('SuppressKnightAbilities',$moduleTask.TypeSystem.Boolean,$playerTypeTask)
    foreach($abilityNameTask in @('CanDoubleJump','CanWallJump')) {
        $abilityTask=$heroTypeTask.Methods | Where-Object Name -EQ $abilityNameTask | Select-Object -First 1
        if(-not $abilityTask) {throw "Missing ability method $abilityNameTask"}
        $abilityIlTask=$abilityTask.Body.GetILProcessor();$abilityFirstTask=$abilityTask.Body.Instructions[0]
        $abilityIlTask.InsertBefore($abilityFirstTask,$abilityIlTask.Create([Mono.Cecil.Cil.OpCodes]::Call,$abilityHookTask))
        $abilityIlTask.InsertBefore($abilityFirstTask,$abilityIlTask.Create([Mono.Cecil.Cil.OpCodes]::Brfalse,$abilityFirstTask))
        $abilityIlTask.InsertBefore($abilityFirstTask,$abilityIlTask.Create([Mono.Cecil.Cil.OpCodes]::Ldc_I4_0))
        $abilityIlTask.InsertBefore($abilityFirstTask,$abilityIlTask.Create([Mono.Cecil.Cil.OpCodes]::Ret))
    }
    foreach($resourceTask in $heroTypeTask.Methods | Where-Object { $_.Name -in @('SoulGain','AddMPCharge') -and $_.ReturnType.FullName -eq 'System.Void' }) {
        $resourceIlTask=$resourceTask.Body.GetILProcessor();$resourceFirstTask=$resourceTask.Body.Instructions[0]
        $resourceIlTask.InsertBefore($resourceFirstTask,$resourceIlTask.Create([Mono.Cecil.Cil.OpCodes]::Call,$resourceHookTask))
        $resourceIlTask.InsertBefore($resourceFirstTask,$resourceIlTask.Create([Mono.Cecil.Cil.OpCodes]::Brfalse,$resourceFirstTask))
        $resourceIlTask.InsertBefore($resourceFirstTask,$resourceIlTask.Create([Mono.Cecil.Cil.OpCodes]::Ret))
    }
    $saveMethodTask=[Mono.Cecil.MethodReference]::new('SaveRoot',$moduleTask.TypeSystem.String,$typeRefTask)
    $saveMethodTask.HasThis=$false
    $redirectCountTask=0
    $typeQueueTask=[Collections.Generic.Queue[Mono.Cecil.TypeDefinition]]::new()
    foreach($typeTask in $moduleTask.Types) { $typeQueueTask.Enqueue($typeTask) }
    while($typeQueueTask.Count -gt 0) {
        $typeTask=$typeQueueTask.Dequeue()
        foreach($nestedTask in $typeTask.NestedTypes) { $typeQueueTask.Enqueue($nestedTask) }
        foreach($methodTask in $typeTask.Methods) {
            if (-not $methodTask.HasBody) { continue }
            foreach($opTask in $methodTask.Body.Instructions) {
                if ($opTask.Operand -is [Mono.Cecil.MethodReference] -and
                    $opTask.Operand.FullName -eq 'System.String UnityEngine.Application::get_persistentDataPath()') {
                    $opTask.Operand=$saveMethodTask
                    $redirectCountTask++
                }
            }
        }
    }
    $gameAssemblyTask.Write((Join-Path $outTask 'Assembly-CSharp.dll'))
    $reportTask=@{
        gameVersion=$versionTask.Constant
        originalSha256=(Get-FileHash -LiteralPath (Join-Path $gameManagedTask 'Assembly-CSharp.dll')).Hash
        patchedSha256=(Get-FileHash -LiteralPath (Join-Path $outTask 'Assembly-CSharp.dll')).Hash
        bootstrap='StartManager.Awake: prepend HollowCraftBootstrap.Initialize()'
        savePathRedirects=$redirectCountTask
    }
    $reportTask | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outTask 'patch-report.json') -Encoding utf8
} finally { $gameAssemblyTask.Dispose(); $resolverTask.Dispose() }
& (Join-Path $PSScriptRoot 'prepare-assets.ps1') -MinecraftJar (Join-Path $configTask.minecraftRoot 'versions\1.20.1\1.20.1.jar')
Write-Output "HollowCraft build ready: $outTask"
