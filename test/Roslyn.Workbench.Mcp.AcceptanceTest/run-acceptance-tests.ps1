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
$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) 'roslyn-workbench-mcp\acceptance'
$publishRoot = Join-Path $temporaryRoot "publish\$(Get-Date -Format 'yyyyMMdd-HHmmss')-$([Guid]::NewGuid().ToString('N'))"
$hostOutput = Join-Path $publishRoot 'host'
$previousHostPath = $env:ROSLYN_WORKBENCH_MCP_ACCEPTANCE_HOST_PATH
$previousSentryDsn = $env:ROSLYN_WORKBENCH_SENTRY_DSN

New-Item -ItemType Directory -Path $publishRoot | Out-Null
Push-Location $repositoryRoot

try
{
    $env:ROSLYN_WORKBENCH_SENTRY_DSN = ''
    Write-Host 'Publishing Roslyn Workbench Host (Release)...'
    Invoke-DotNet -Arguments @(
        'publish',
        'src/Roslyn.Workbench.Mcp/Roslyn.Workbench.Mcp.csproj',
        '--configuration', 'Release',
        '--output', $hostOutput
    )

    $hostPath = Join-Path $hostOutput 'Roslyn.Workbench.Mcp.exe'
    if (-not (Test-Path $hostPath -PathType Leaf))
    {
        throw "The published Host was not found at '$hostPath'."
    }

    Write-Host "Temporary published binaries: $publishRoot"
    Write-Host 'Running published Host acceptance tests...'
    $env:ROSLYN_WORKBENCH_MCP_ACCEPTANCE_HOST_PATH = $hostPath

    $testArguments = @(
        'test',
        'test/Roslyn.Workbench.Mcp.AcceptanceTest/Roslyn.Workbench.Mcp.AcceptanceTest.csproj',
        '--configuration', 'Release'
    ) + $args

    Invoke-DotNet -Arguments $testArguments
}
finally
{
    $env:ROSLYN_WORKBENCH_MCP_ACCEPTANCE_HOST_PATH = $previousHostPath
    $env:ROSLYN_WORKBENCH_SENTRY_DSN = $previousSentryDsn
    Pop-Location

    if (Test-Path $publishRoot)
    {
        Remove-Item -Recurse -Force $publishRoot
    }
}
