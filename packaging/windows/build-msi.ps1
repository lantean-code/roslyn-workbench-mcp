#requires -Version 7.0
<#
.SYNOPSIS
Publishes the framework-dependent Windows Host and builds an unsigned x64 MSI.
.DESCRIPTION
Run with the Windows .NET SDK. Normal development defaults to 0.0.0-dev and never
runs GitVersion. Release automation can supply the existing release identity via
environment properties. Each build uses a fresh directory to exclude stale files.
#>
[CmdletBinding()]
param(
    [string]$Version = $env:RoslynWorkbenchVersion,
    [string]$OutputDirectory,
    [string]$DotNetPath = 'dotnet'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) {
    throw 'Run build-msi.ps1 with PowerShell 7 and the .NET SDK on Windows.'
}
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = '0.0.0-dev'
}
if ($Version -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-[0-9A-Za-z.-]+)?$') {
    throw 'Version must be a semantic version without build metadata.'
}
$numericVersion = [version]::new([int]$Matches[1], [int]$Matches[2], [int]$Matches[3])
if ($numericVersion.Major -gt 255 -or $numericVersion.Minor -gt 255 -or $numericVersion.Build -gt 65535) {
    throw 'The numeric version exceeds the Windows Installer limits (255.255.65535).'
}
if ($env:RoslynWorkbenchVersion -and $env:RoslynWorkbenchVersion -ne $Version) {
    throw 'Version must match the release identity already supplied by the environment.'
}

$repositoryRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not $OutputDirectory) {
    $buildName = '{0}-{1}' -f $Version, [guid]::NewGuid().ToString('N')
    $OutputDirectory = Join-Path $repositoryRoot "artifacts\installer\$buildName"
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) {
    throw 'OutputDirectory must not exist. Use a new directory for each MSI build.'
}
$publishDirectory = Join-Path $OutputDirectory 'publish'
$msiDirectory = Join-Path $OutputDirectory 'msi'
New-Item -ItemType Directory -Path $publishDirectory, $msiDirectory | Out-Null

Push-Location $repositoryRoot
try {
    $hostProject = Join-Path $repositoryRoot 'src\Roslyn.Workbench.Mcp\Roslyn.Workbench.Mcp.csproj'
    & $DotNetPath publish $hostProject --configuration Release --runtime win-x64 --self-contained false `
        --output $publishDirectory '-p:PackAsTool=false' '-p:UseAppHost=true' "-p:RoslynWorkbenchVersion=$Version"
    if ($LASTEXITCODE -ne 0) {
        throw "Host publication failed with exit code $LASTEXITCODE."
    }

    # Renaming the native apphost keeps the tool command stable; its embedded DLL
    # name remains Roslyn.Workbench.Mcp.dll and assembly identities are unchanged.
    $executable = Join-Path $publishDirectory 'roslyn-workbench-mcp.exe'
    Move-Item -LiteralPath (Join-Path $publishDirectory 'Roslyn.Workbench.Mcp.exe') -Destination $executable
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE') -Destination $publishDirectory
    $actualVersion = & $executable --version
    if ($LASTEXITCODE -ne 0 -or $actualVersion -ne $Version) {
        throw "Published Host identity does not match $Version."
    }

    $licence = Get-Content -LiteralPath (Join-Path $repositoryRoot 'LICENSE') -Raw
    $licence = $licence.Replace('\', '\\').Replace('{', '\{').Replace('}', '\}')
    $licence = $licence -replace '\r?\n', '\par '
    $licenceRtf = Join-Path $OutputDirectory 'licence.rtf'
    Set-Content -LiteralPath $licenceRtf -Value "{\rtf1\ansi\deff0{\fonttbl{\f0 Segoe UI;}}\f0\fs18 $licence}" -Encoding ascii

    $installerProject = Join-Path $PSScriptRoot 'Roslyn.Workbench.Mcp.Setup.wixproj'
    & $DotNetPath build $installerProject --configuration Release `
        "-p:HostPublishDirectory=$publishDirectory" "-p:HostVersion=$Version" `
        "-p:MsiVersion=$numericVersion" "-p:LicenseRtf=$licenceRtf" "-p:OutputPath=$msiDirectory\" `
        "-p:BaseIntermediateOutputPath=$OutputDirectory\obj\"
    if ($LASTEXITCODE -ne 0) {
        throw "MSI build failed with exit code $LASTEXITCODE."
    }

    $msiPath = Join-Path $msiDirectory "roslyn-workbench-mcp-$Version-win-x64.msi"
    $hash = Get-FileHash -LiteralPath $msiPath -Algorithm SHA256
    Set-Content -LiteralPath "$msiPath.sha256" -Value "$($hash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($msiPath))" -Encoding ascii
    Write-Host "Unsigned MSI: $msiPath"
    Write-Host 'Signing and public release publication are deliberately separate steps.'
}
finally {
    Pop-Location
}
