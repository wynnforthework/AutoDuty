[CmdletBinding()]
param([switch]$BuildOnly, [switch]$PlanOnly)
$ErrorActionPreference = 'Stop'
$skillScript = Join-Path $env:USERPROFILE '.codex/skills/dalamud-plugin-dev/scripts/build-plugin.ps1'
if (-not (Test-Path -LiteralPath $skillScript)) {
    $skillScript = Join-Path $PSScriptRoot '../skills/dalamud-plugin-dev/scripts/build-plugin.ps1'
}
if (-not (Test-Path -LiteralPath $skillScript)) { throw "Missing dalamud-plugin-dev build script: $skillScript" }
& $skillScript -Plugin AutoDuty -Project (Join-Path $PSScriptRoot '../AutoDuty/AutoDuty.csproj') -BuildOnly:$BuildOnly -PlanOnly:$PlanOnly
