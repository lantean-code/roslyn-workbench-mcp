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
$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) 'rwmcp'
$publishRoot = Join-Path $temporaryRoot "p\$(Get-Date -Format 'yyyyMMdd-HHmmss')-$([Guid]::NewGuid().ToString('N'))"
$hostOutput = Join-Path $publishRoot 'host'
$runnerOutput = Join-Path $publishRoot 'runner'
$pluginOutput = Join-Path $publishRoot 'plugins\host-query'
$pluginRoot = Join-Path $publishRoot 'plugins'
$runnerArguments = $args
$previousSentryDsn = $env:ROSLYN_WORKBENCH_SENTRY_DSN

if ($runnerArguments.Count -eq 0)
{
    $runnerArguments = @('list')
}

New-Item -ItemType Directory -Path $publishRoot | Out-Null
Push-Location $repositoryRoot

try
{
    $env:ROSLYN_WORKBENCH_SENTRY_DSN = ''
    Write-Host 'Restoring pinned diagnostic tools...'
    Invoke-DotNet -Arguments @('tool', 'restore')

    Write-Host 'Publishing Roslyn Workbench Host (Release)...'
    Invoke-DotNet -Arguments @(
        'publish',
        'src/Roslyn.Workbench.Mcp/Roslyn.Workbench.Mcp.csproj',
        '--configuration', 'Release',
        '--output', $hostOutput
    )

    Write-Host 'Publishing scenario runner (Release)...'
    Invoke-DotNet -Arguments @(
        'publish',
        'tools/Roslyn.Workbench.Mcp.ScenarioRunner/Roslyn.Workbench.Mcp.ScenarioRunner.csproj',
        '--configuration', 'Release',
        '--output', $runnerOutput
    )

    Write-Host 'Publishing cache-calibration plugin (Release)...'
    Invoke-DotNet -Arguments @(
        'publish',
        'test/TestFixtures/Plugins/Roslyn.Workbench.Mcp.HostQueryPluginFixture/Roslyn.Workbench.Mcp.HostQueryPluginFixture.csproj',
        '--configuration', 'Release',
        '--output', $pluginOutput
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

    $runnerPath = Join-Path $runnerOutput 'Roslyn.Workbench.Mcp.ScenarioRunner.exe'
    Write-Host "Temporary published binaries: $publishRoot"

    if (Test-Path $runnerPath -PathType Leaf)
    {
        & $runnerPath @runnerArguments --host $hostPath --framework-root $repositoryRoot --plugin-directory $pluginRoot
    }
    else
    {
        $runnerDll = Join-Path $runnerOutput 'Roslyn.Workbench.Mcp.ScenarioRunner.dll'
        & $dotnetPath $runnerDll @runnerArguments --host $hostPath --framework-root $repositoryRoot --plugin-directory $pluginRoot
    }

    $runnerExitCode = $LASTEXITCODE
}
finally
{
    $env:ROSLYN_WORKBENCH_SENTRY_DSN = $previousSentryDsn
    Pop-Location

    if (Test-Path $publishRoot)
    {
        Remove-Item -Recurse -Force $publishRoot
    }
}

exit $runnerExitCode
