param([string]$Gradle, [string]$JavaHome = $env:JAVA_HOME)
$ErrorActionPreference = 'Stop'
$projectTaskRoot = Split-Path $PSScriptRoot -Parent
if (-not $Gradle) {
    $gradleCommandTask = Get-Command gradle.bat -ErrorAction SilentlyContinue
    if ($gradleCommandTask) { $Gradle = $gradleCommandTask.Source }
    else {
        $gradleCacheTask = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.gradle\wrapper\dists\gradle-8.10-bin'
        $Gradle = Get-ChildItem -LiteralPath $gradleCacheTask -Filter gradle.bat -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
    }
}
if (-not $Gradle -or -not (Test-Path -LiteralPath $Gradle)) { throw 'Gradle 8.10 not found. Supply -Gradle <gradle.bat path>.' }
if (-not $JavaHome) {
    $javaInstallTask = Get-ChildItem -LiteralPath 'C:\Program Files\Microsoft' -Directory -Filter 'jdk-21*' -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -First 1
    if ($javaInstallTask) { $JavaHome = $javaInstallTask.FullName }
}
if (-not $JavaHome -or -not (Test-Path -LiteralPath (Join-Path $JavaHome 'bin\java.exe'))) { throw 'JDK not found. Supply -JavaHome <JDK directory>.' }
$env:JAVA_HOME = $JavaHome
$env:GRADLE_USER_HOME = Join-Path $projectTaskRoot 'dependencies\gradle-home'
Push-Location (Join-Path $projectTaskRoot 'minecraft')
try {
    & $Gradle --no-daemon build
    if ($LASTEXITCODE -ne 0) { throw 'Minecraft bridge build failed' }
} finally { Pop-Location }
