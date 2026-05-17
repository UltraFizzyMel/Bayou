param(
  [string]$ProjectPath = "$(Join-Path $PSScriptRoot 'Bayou')",
  [ValidateSet('Win64','Linux64','OSX','WebGL')]
  [string]$Target = 'Win64',
  [string]$Output = '',
  [switch]$Development,
  [string]$Version = '',
  [string]$UnityExe = '',
  [string]$LogFile = ''
)

$ErrorActionPreference = 'Stop'

function Get-UnityVersionFromProject([string]$projectPath) {
  $pv = Join-Path $projectPath 'ProjectSettings\ProjectVersion.txt'
  if (-not (Test-Path $pv)) { return '' }
  $line = (Get-Content $pv | Select-Object -First 1)
  if ($line -match 'm_EditorVersion:\s*(.+)\s*$') { return $Matches[1] }
  return ''
}

function Resolve-UnityExe([string]$explicitExe, [string]$projectPath) {
  if ($explicitExe) {
    if (-not (Test-Path $explicitExe)) { throw "UnityExe not found: $explicitExe" }
    return (Resolve-Path $explicitExe).Path
  }

  if ($env:UNITY_PATH) {
    if (-not (Test-Path $env:UNITY_PATH)) { throw "UNITY_PATH points to missing file: $env:UNITY_PATH" }
    return (Resolve-Path $env:UNITY_PATH).Path
  }

  $version = Get-UnityVersionFromProject $projectPath
  if (-not $version) { throw "Could not read Unity version from ProjectSettings\ProjectVersion.txt" }

  $hubDefault = Join-Path ${env:ProgramFiles} "Unity\Hub\Editor\$version\Editor\Unity.exe"
  if (Test-Path $hubDefault) { return (Resolve-Path $hubDefault).Path }

  $hubDefaultX86 = Join-Path ${env:ProgramFiles(x86)} "Unity\Hub\Editor\$version\Editor\Unity.exe"
  if (Test-Path $hubDefaultX86) { return (Resolve-Path $hubDefaultX86).Path }

  throw @"
Unable to find Unity Editor $version.
Tried:
  $hubDefault
  $hubDefaultX86

Fix:
  - Install that editor version in Unity Hub, or
  - pass -UnityExe 'C:\path\to\Unity.exe', or
  - set env var UNITY_PATH to Unity.exe
"@
}

if (-not (Test-Path $ProjectPath)) { throw "ProjectPath not found: $ProjectPath" }

$lockFile = Join-Path $ProjectPath 'Temp\UnityLockfile'
if (Test-Path $lockFile) {
  throw @"
This Unity project appears to be open (lock file exists):
  $lockFile

Close Unity (and Rider/VS Unity integration if it's holding the lock) and re-run the build.
"@
}

$unity = Resolve-UnityExe -explicitExe $UnityExe -projectPath $ProjectPath

if (-not $Output) {
  $Output = Join-Path $PSScriptRoot (Join-Path 'artifacts' $Target)
}

if (-not $LogFile) {
  $LogFile = Join-Path $PSScriptRoot (Join-Path 'artifacts' (Join-Path 'logs' "unity-build-$Target.log"))
}
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $LogFile) | Out-Null
New-Item -ItemType Directory -Force -Path $Output | Out-Null

$args = @(
  '-batchmode',
  '-nographics',
  '-quit',
  '-projectPath', $ProjectPath,
  '-executeMethod', 'Bayou.Build.BuildPlayer.BuildFromCommandLine',
  '-logFile', $LogFile,
  "-bayouBuild:target=$Target",
  "-bayouBuild:output=$Output",
  "-bayouBuild:development=$($Development.IsPresent)"
)

if ($Version) {
  $args += "-bayouBuild:version=$Version"
}

Write-Host "Unity: $unity"
Write-Host "Project: $ProjectPath"
Write-Host "Target: $Target"
Write-Host "Output: $Output"
Write-Host "Log: $LogFile"

& $unity @args
$code = if ($null -eq $LASTEXITCODE) { 1 } else { $LASTEXITCODE }
if ($code -ne 0) {
  throw "Unity build failed (exit code $code). See log: $LogFile"
}

Write-Host "Build complete."

