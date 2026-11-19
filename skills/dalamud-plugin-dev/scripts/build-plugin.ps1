[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Plugin,
    [string]$Project,
    [string]$EnvironmentFile = (Join-Path $PSScriptRoot '../references/local-environment.json'),
    [switch]$BuildOnly,
    [switch]$PlanOnly
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-File([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Required file missing: $Path" }
}

function Install-File([string]$Source, [string]$Destination) {
    if ((Test-Path -LiteralPath $Destination -PathType Leaf) -and
        (Get-FileHash -LiteralPath $Source).Hash -eq (Get-FileHash -LiteralPath $Destination).Hash) { return }
    $parent = Split-Path -Parent $Destination
    [void][IO.Directory]::CreateDirectory($parent)
    $temporary = Join-Path $parent ('.' + [IO.Path]::GetFileName($Destination) + '.deploying')
    try {
        [IO.File]::Copy($Source, $temporary, $true)
        if ([IO.File]::Exists($Destination)) { [IO.File]::Replace($temporary, $Destination, [NullString]::Value) }
        else { [IO.File]::Move($temporary, $Destination) }
    } catch {
        throw "Cannot update fixed file '$Destination': $($_.Exception.Message). Keep this path; do not create another plugin entry."
    } finally {
        if ([IO.File]::Exists($temporary)) { [IO.File]::Delete($temporary) }
    }
}

Assert-File $EnvironmentFile
$environment = Get-Content -LiteralPath $EnvironmentFile -Raw -Encoding UTF8 | ConvertFrom-Json
$entryProperty = $environment.Plugins.PSObject.Properties[$Plugin]
if ($null -eq $entryProperty) { throw "Plugin '$Plugin' is not mapped. Record its stable paths in $EnvironmentFile first." }
$entry = $entryProperty.Value
if (-not $Project) { $Project = $entry.Project }
Assert-File $Project
Assert-File $environment.Dotnet
Assert-File (Join-Path $environment.DalamudLibPath 'Dalamud.dll')
Assert-File (Join-Path $environment.DalamudLibPath 'Dalamud.xml')
$projectPath = (Resolve-Path -LiteralPath $Project).Path
if (-not [IO.Path]::IsPathRooted($entry.DeployDirectory)) { throw 'DeployDirectory must be absolute.' }
$destination = [IO.Path]::GetFullPath($entry.DeployDirectory)
$dllName = $entry.InternalName + '.dll'
$mainDestination = Join-Path $destination $dllName
Write-Host "Project: $projectPath"
Write-Host "Dotnet: $($environment.Dotnet)"
Write-Host "Dalamud: $($environment.DalamudLibPath)"
Write-Host "Fixed DLL: $mainDestination"
if ($PlanOnly) { return }

# A single fixed destination also serializes deployment from worktrees.
[void][IO.Directory]::CreateDirectory($destination)
$lock = $null
try {
    $lock = [IO.File]::Open((Join-Path $destination '.build-deploy.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
    & $environment.Dotnet build $projectPath -c Release "-p:DalamudLibPath=$($environment.DalamudLibPath)"
    if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE); fixed DLL was not deployed." }
    $query = & $environment.Dotnet msbuild $projectPath -nologo '-p:Configuration=Release' "-p:DalamudLibPath=$($environment.DalamudLibPath)" '-getProperty:TargetPath,AssemblyName'
    if ($LASTEXITCODE -ne 0) { throw 'Cannot query build output.' }
    $properties = (($query -join [Environment]::NewLine) | ConvertFrom-Json).Properties
    if ($properties.AssemblyName -ne $entry.InternalName) { throw 'AssemblyName does not match the fixed plugin identity.' }
    $builtDll = $properties.TargetPath
    Assert-File $builtDll
    $output = Split-Path -Parent $builtDll
    $manifestFile = Join-Path $output ($entry.InternalName + '.json')
    Assert-File $manifestFile
    $manifest = Get-Content -LiteralPath $manifestFile -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($manifest.InternalName -ne $entry.InternalName) { throw 'Manifest InternalName does not match the fixed plugin identity.' }
    $builtVersion = [Reflection.AssemblyName]::GetAssemblyName($builtDll).Version.ToString()
    if ($builtVersion -ne $manifest.AssemblyVersion) { throw 'Built DLL version differs from manifest.' }
    if ([IO.Path]::GetFullPath($output).TrimEnd('\','/') -eq $destination.TrimEnd('\','/')) {
        throw 'Build output must be separate from the live deployment directory.'
    }
    if ($BuildOnly) { Write-Host "Build verified; no deployment: $builtDll"; return }

    # Ignore SDK's additional distribution directory; use actual TargetPath.
    $excludedDirectories = @($entry.InternalName, 'ref', 'refint', 'obj', 'bin')
    foreach ($item in Get-ChildItem -LiteralPath $output -Force) {
        if ($item.PSIsContainer) {
            if ($item.Name -in $excludedDirectories) { continue }
            foreach ($file in Get-ChildItem -LiteralPath $item.FullName -Recurse -File) {
                $relative = $file.FullName.Substring($output.Length).TrimStart('\','/')
                Install-File $file.FullName (Join-Path $destination $relative)
            }
        } elseif ($item.FullName -ne $builtDll -and $item.Extension -notin @('.zip', '.nupkg', '.snupkg', '.cache')) {
            Install-File $item.FullName (Join-Path $destination $item.Name)
        }
    }
    Install-File $builtDll $mainDestination
    $sourceHash = (Get-FileHash -LiteralPath $builtDll).Hash
    $deployedHash = (Get-FileHash -LiteralPath $mainDestination).Hash
    if ($sourceHash -ne $deployedHash) { throw 'Deployed DLL hash mismatch.' }
    $deployedVersion = [Reflection.AssemblyName]::GetAssemblyName($mainDestination).Version.ToString()
    if ($deployedVersion -ne $builtVersion) { throw 'Deployed DLL version mismatch.' }
    $receipt = [ordered]@{
        VerifiedUtc = [DateTime]::UtcNow.ToString('o')
        Project = $projectPath
        BuildDll = $builtDll
        DeployDirectory = $destination
        DeployedDll = $mainDestination
        AssemblyVersion = $deployedVersion
        ProductVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($mainDestination).ProductVersion
        SHA256 = $deployedHash
        Bytes = (Get-Item -LiteralPath $mainDestination).Length
        LastWriteTimeUtc = (Get-Item -LiteralPath $mainDestination).LastWriteTimeUtc.ToString('o')
        GameReloadVerified = $false
    }
    [IO.File]::WriteAllText((Join-Path $destination 'build-result.json'), ($receipt | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
    Write-Host "Updated fixed DLL: $mainDestination"
    Write-Host "Verified DLL version: $deployedVersion; SHA256: $deployedHash"
    Write-Host 'File deployment verified. In-game reload requires this exact entry with AutomaticReloading enabled.'
} finally {
    if ($null -ne $lock) { $lock.Dispose() }
}
