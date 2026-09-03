#requires -Version 7.0
<#
.SYNOPSIS
Builds an unsigned development MSIX around the framework-dependent Windows Host.
.DESCRIPTION
Uses Visual Studio's Windows Application Packaging tools, without installing the
package, trusting a certificate, running GitVersion or contacting the Store.
#>
[CmdletBinding()]
param(
    [string]$Version = $env:RoslynWorkbenchVersion,
    [string]$OutputDirectory,
    [string]$DotNetPath = 'dotnet',
    [string]$MSBuildPath,
    [string]$WindowsSdkVersion = '10.0.28000.0'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) {
    throw 'Run build-msix.ps1 with PowerShell 7 on Windows.'
}
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = '0.0.0-dev'
}
if ($Version -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-[0-9A-Za-z.-]+)?$') {
    throw 'Version must be a semantic version without build metadata.'
}
$packageVersion = [version]::new([int]$Matches[1], [int]$Matches[2], [int]$Matches[3], 0)
if ($packageVersion.Major -gt 65535 -or $packageVersion.Minor -gt 65535 -or $packageVersion.Build -gt 65535) {
    throw 'MSIX version components must not exceed 65535.'
}
if ($env:RoslynWorkbenchVersion -and $env:RoslynWorkbenchVersion -ne $Version) {
    throw 'Version must match the release identity already supplied by the environment.'
}
if (-not $MSBuildPath) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path -LiteralPath $vswhere)) {
        throw 'Install Visual Studio with MSIX Packaging Tools and the Windows SDK, or supply MSBuildPath.'
    }
    $visualStudio = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.ComponentGroup.MSIX.Packaging -property installationPath
    if ($LASTEXITCODE -ne 0 -or -not $visualStudio) {
        throw 'Visual Studio MSIX Packaging Tools were not found.'
    }
    $MSBuildPath = Join-Path $visualStudio 'MSBuild\Current\Bin\MSBuild.exe'
}
if (-not (Test-Path -LiteralPath $MSBuildPath)) {
    throw 'MSBuildPath must identify Visual Studio MSBuild.exe, not dotnet MSBuild.'
}

$repositoryRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not $OutputDirectory) {
    $buildName = '{0}-{1}' -f $Version, [guid]::NewGuid().ToString('N')
    $OutputDirectory = Join-Path $repositoryRoot "artifacts\msix\$buildName"
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) {
    throw 'OutputDirectory must not exist. Use a new directory for each MSIX build.'
}
$publishDirectory = Join-Path $OutputDirectory 'publish'
$packageDirectory = Join-Path $OutputDirectory 'package'
New-Item -ItemType Directory -Path $publishDirectory, $packageDirectory | Out-Null

Push-Location $repositoryRoot
try {
    $hostProject = Join-Path $repositoryRoot 'src\Roslyn.Workbench.Mcp\Roslyn.Workbench.Mcp.csproj'
    & $DotNetPath publish $hostProject --configuration Release --runtime win-x64 --self-contained false `
        --output $publishDirectory '-p:PackAsTool=false' '-p:UseAppHost=true' "-p:RoslynWorkbenchVersion=$Version"
    if ($LASTEXITCODE -ne 0) {
        throw "Host publication failed with exit code $LASTEXITCODE."
    }
    $executable = Join-Path $publishDirectory 'roslyn-workbench-mcp.exe'
    Move-Item -LiteralPath (Join-Path $publishDirectory 'Roslyn.Workbench.Mcp.exe') -Destination $executable
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE') -Destination $publishDirectory
    $actualVersion = & $executable --version
    if ($LASTEXITCODE -ne 0 -or $actualVersion -ne $Version) {
        throw "Published Host identity does not match $Version."
    }

    # Change a generated copy only; local builds never edit the checked-in identity.
    $manifestPath = Join-Path $OutputDirectory 'Package.appxmanifest'
    [xml]$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'msix\Package.appxmanifest') -Raw
    $manifest.Package.Identity.Version = $packageVersion.ToString()
    $manifest.Package.Dependencies.TargetDeviceFamily.MaxVersionTested = $WindowsSdkVersion
    $manifest.Save($manifestPath)

    & $MSBuildPath (Join-Path $PSScriptRoot 'msix\Roslyn.Workbench.Mcp.Package.wapproj') /restore /t:Build `
        /p:Configuration=Release /p:Platform=x64 /p:GenerateAppxPackageOnBuild=true `
        "/p:TargetPlatformVersion=$WindowsSdkVersion" "/p:HostPublishDirectory=$publishDirectory" `
        "/p:PackageManifest=$manifestPath" `
        "/p:AppxPackageDir=$packageDirectory\" "/p:OutputPath=$OutputDirectory\bin\" `
        "/p:BaseIntermediateOutputPath=$OutputDirectory\obj\"
    if ($LASTEXITCODE -ne 0) {
        throw "MSIX build failed with exit code $LASTEXITCODE."
    }
    $packages = @(Get-ChildItem -LiteralPath $packageDirectory -Recurse -Filter '*.msix')
    if ($packages.Count -ne 1) {
        throw "Expected exactly one MSIX, found $($packages.Count)."
    }
    $msixPath = Join-Path $OutputDirectory "roslyn-workbench-mcp-$Version-win-x64.msix"
    Copy-Item -LiteralPath $packages[0].FullName -Destination $msixPath
    # Packaging defaults can silently exclude files such as PDBs. Verify the actual
    # archive retains every published Host file, not just its entry-point executable.
    $archive = [IO.Compression.ZipFile]::OpenRead($msixPath)
    try {
        foreach ($file in Get-ChildItem -LiteralPath $publishDirectory -Recurse -File) {
            $relativePath = [IO.Path]::GetRelativePath($publishDirectory, $file.FullName).Replace('\', '/')
            $entry = $archive.GetEntry("Host/$relativePath")
            if (-not $entry) {
                throw "MSIX is missing published Host file: $relativePath"
            }
            $stream = $entry.Open()
            try {
                $packagedHash = (Get-FileHash -InputStream $stream -Algorithm SHA256).Hash
                $sourceHash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
                if ($packagedHash -ne $sourceHash) {
                    throw "MSIX changed published Host file: $relativePath"
                }
            }
            finally {
                $stream.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
    }
    $hash = Get-FileHash -LiteralPath $msixPath -Algorithm SHA256
    Set-Content -LiteralPath "$msixPath.sha256" -Value "$($hash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($msixPath))" -Encoding ascii
    Write-Host "Unsigned development MSIX: $msixPath"
    Write-Host 'Not installable until signed and trusted. Store identity and installed compatibility remain separate checks.'
}
finally {
    Pop-Location
}
