$hollowCraftTaskRoot=Split-Path $PSScriptRoot -Parent
function Get-HollowCraftConfigTask {
    $fileTask=Join-Path $hollowCraftTaskRoot 'hollowcraft.local.json'
    if(Test-Path -LiteralPath $fileTask) {
        $configTask=Get-Content -LiteralPath $fileTask -Raw -Encoding UTF8 | ConvertFrom-Json
        if($configTask.schema -ne 1) {throw 'Unsupported HollowCraft configuration. Run Install HollowCraft.cmd.'}
        return $configTask
    }
    return [pscustomobject]@{
        schema=1; portable=$false
        gameRoot='C:\Program Files (x86)\Steam\steamapps\common\Hollow Knight'
        minecraftRoot='C:\Program Files (x86)\MC\.minecraft'
        javaPath='C:\Program Files\Microsoft\jdk-21.0.10.7-hotspot\bin\java.exe'
        nativesPath='C:\Program Files (x86)\MC\.minecraft\versions\1.20.1\1.20.1-natives'
    }
}
function Assert-HollowCraftStoppedTask {
    foreach($componentTask in @('minecraft','hollow-knight')) {
        $recordTaskPath=Join-Path $hollowCraftTaskRoot ('runtime/'+$componentTask+'-process.json')
        if(-not(Test-Path -LiteralPath $recordTaskPath)) {continue}
        $recordTask=Get-Content -LiteralPath $recordTaskPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $processTask=Get-Process -Id $recordTask.id -ErrorAction SilentlyContinue
        if($processTask -and $processTask.StartTime.ToUniversalTime().Ticks -eq $recordTask.startedTicks) {
            throw 'Close HollowCraft with Stop HollowCraft.cmd before installing or updating.'
        }
    }
}
function Test-HollowCraftInsideTask([string]$Child,[string]$Parent) {
    $childTask=[IO.Path]::GetFullPath($Child).TrimEnd('\','/')
    $parentTask=[IO.Path]::GetFullPath($Parent).TrimEnd('\','/')
    return $childTask.Equals($parentTask,[StringComparison]::OrdinalIgnoreCase) -or $childTask.StartsWith($parentTask+'\',[StringComparison]::OrdinalIgnoreCase)
}
function Get-HollowCraftJavaSettingsTask([string]$JavaPath) {
    $savedPreferenceTask=$ErrorActionPreference
    try {
        $ErrorActionPreference='Continue'
        return (& $JavaPath -XshowSettings:properties -version 2>&1 | Out-String)
    } finally {$ErrorActionPreference=$savedPreferenceTask}
}
