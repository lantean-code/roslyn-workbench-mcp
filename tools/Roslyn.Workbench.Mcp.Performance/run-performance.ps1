$ErrorActionPreference = 'Stop'
$dotnetPath = (Get-Command dotnet -ErrorAction SilentlyContinue).Source

if ([string]::IsNullOrWhiteSpace($dotnetPath))
{
    $dotnetPath = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
}

if (-not (Test-Path $dotnetPath -PathType Leaf))
{
    throw 'The .NET SDK executable was not found on PATH or under Program Files\dotnet.'
}

function Invoke-DotNet
{
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Arguments
    )

    & $script:dotnetPath @Arguments
    if ($LASTEXITCODE -ne 0)
    {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$publishRoot = Join-Path $repositoryRoot "artifacts\performance\publish\$(Get-Date -Format 'yyyyMMdd-HHmmss')-$([Guid]::NewGuid().ToString('N'))"
$hostOutput = Join-Path $publishRoot 'host'
$runnerOutput = Join-Path $publishRoot 'runner'
$runnerArguments = $args

if ($runnerArguments.Count -eq 0)
{
    $runnerArguments = @('list')
}

New-Item -ItemType Directory -Path $publishRoot | Out-Null
Push-Location $repositoryRoot

try
{
    Write-Host 'Restoring pinned diagnostic tools...'
    Invoke-DotNet -Arguments @('tool', 'restore')

    Write-Host 'Publishing Roslyn Workbench Host (Release)...'
    Invoke-DotNet -Arguments @(
        'publish',
        'src/Roslyn.Workbench.Mcp/Roslyn.Workbench.Mcp.csproj',
        '--configuration', 'Release',
        '--output', $hostOutput
    )

    Write-Host 'Publishing performance runner (Release)...'
    Invoke-DotNet -Arguments @(
        'publish',
        'tools/Roslyn.Workbench.Mcp.Performance/Roslyn.Workbench.Mcp.Performance.csproj',
        '--configuration', 'Release',
        '--output', $runnerOutput
    )

    $hostPath = Join-Path $hostOutput 'Roslyn.Workbench.Mcp.exe'
    if (-not (Test-Path $hostPath -PathType Leaf))
    {
        $hostPath = Join-Path $hostOutput 'Roslyn.Workbench.Mcp.dll'
    }

    if (-not (Test-Path $hostPath -PathType Leaf))
    {
        throw "The published Host was not found beneath '$hostOutput'."
    }

    $runnerPath = Join-Path $runnerOutput 'Roslyn.Workbench.Mcp.Performance.exe'
    Write-Host "Published binaries: $publishRoot"

    if (Test-Path $runnerPath -PathType Leaf)
    {
        & $runnerPath @runnerArguments --host $hostPath --framework-root $repositoryRoot
    }
    else
    {
        $runnerDll = Join-Path $runnerOutput 'Roslyn.Workbench.Mcp.Performance.dll'
        & $dotnetPath $runnerDll @runnerArguments --host $hostPath --framework-root $repositoryRoot
    }

    $runnerExitCode = $LASTEXITCODE
}
finally
{
    Pop-Location
}

exit $runnerExitCode
