param([string]$EditorPath)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'LOF'
$versionFile = Join-Path $projectPath 'ProjectSettings/ProjectVersion.txt'
$versionLine = Get-Content -LiteralPath $versionFile | Where-Object { $_ -match '^m_EditorVersion: ' }
$requiredVersion = ($versionLine -split ': ', 2)[1].Trim()

if (-not $EditorPath) {
    $EditorPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$requiredVersion/Editor/Unity.exe"
}
if (-not (Test-Path -LiteralPath $EditorPath -PathType Leaf)) {
    throw "Install Unity $requiredVersion through Hub, or pass -EditorPath with the Unity.exe path."
}
# Unity is an interactive application; open its normal editor window.
Start-Process -FilePath $EditorPath -ArgumentList @('-projectPath', ('"{0}"' -f $projectPath))
