param(
    [Parameter(Mandatory = $true)][string]$Ref,
    [string]$OutDir = "$PSScriptRoot/../protos/jobforge/v1"
)
$ErrorActionPreference = "Stop"
$url = "https://raw.githubusercontent.com/TiagoR85/jobforge-orchestrator/$Ref/src/JobForge.Orchestrator/Protos/jobforge/v1/status.proto"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$dest = Join-Path $OutDir "status.proto"
Invoke-WebRequest -Uri $url -OutFile $dest
Write-Host "Fetched status.proto from $Ref ($((Get-Item $dest).Length) bytes)"
