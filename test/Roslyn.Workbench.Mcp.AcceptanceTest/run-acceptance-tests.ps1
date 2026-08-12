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

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Push-Location $repositoryRoot

try
{
    $testArguments = @(
        'test',
        'test/Roslyn.Workbench.Mcp.AcceptanceTest/Roslyn.Workbench.Mcp.AcceptanceTest.csproj',
        '--configuration', 'Release'
    ) + $args

    & $dotnetPath @testArguments
    if ($LASTEXITCODE -ne 0)
    {
        throw "dotnet $($testArguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}
finally
{
    Pop-Location
}
