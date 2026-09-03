#requires -Version 7.0
<#
.SYNOPSIS
Checks MSI metadata and upgrade sequencing, with optional per-user installation.
.DESCRIPTION
The default is read-only inspection. -Install tests installation, the installed
command, repair and removal. It refuses an existing installation and never opts
into PATH changes. Use an isolated Windows machine for all-users and UI checks.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$MsiPath,
    [Parameter(Mandatory)][string]$ExpectedVersion,
    [switch]$Install
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) {
    throw 'MSI validation requires Windows.'
}
$MsiPath = (Resolve-Path -LiteralPath $MsiPath).Path
$installer = New-Object -ComObject WindowsInstaller.Installer
$database = $installer.OpenDatabase($MsiPath, 0)

function Get-MsiProperty([string]$Name) {
    $view = $database.OpenView("SELECT ``Value`` FROM ``Property`` WHERE ``Property`` = '$Name'")
    try {
        $view.Execute() | Out-Null
        $record = $view.Fetch()
        if ($null -eq $record) {
            throw "MSI property '$Name' is missing."
        }
        return $record.StringData(1)
    }
    finally {
        $view.Close() | Out-Null
    }
}

function Invoke-Msi([string[]]$Arguments, [string]$LogName) {
    $logPath = Join-Path $logDirectory "$LogName.log"
    $process = Start-Process -FilePath "$env:SystemRoot\System32\msiexec.exe" `
        -ArgumentList ($Arguments + @('/qn', '/norestart', '/l*v', "`"$logPath`"")) -Wait -PassThru
    if ($process.ExitCode -notin @(0, 3010)) {
        throw "MSI $LogName failed with exit code $($process.ExitCode). See $logPath."
    }
    if ($process.ExitCode -eq 3010) {
        Write-Warning "MSI $LogName succeeded but requested a restart; no restart was performed."
    }
}

function Get-MsiActionSequence([string]$Table, [string]$Action) {
    $view = $database.OpenView("SELECT ``Sequence`` FROM ``$Table`` WHERE ``Action`` = '$Action'")
    try {
        $view.Execute() | Out-Null
        $record = $view.Fetch()
        if ($null -eq $record) {
            throw "MSI action '$Action' is missing from $Table."
        }
        return $record.IntegerData(1)
    }
    finally {
        $view.Close() | Out-Null
    }
}

function Assert-MsiActionOrder([string]$Table, [string[]]$Actions) {
    $previousSequence = 0
    $previousAction = 'start of sequence'
    foreach ($action in $Actions) {
        $sequence = Get-MsiActionSequence $Table $action
        if ($sequence -le $previousSequence) {
            throw "MSI $Table must schedule '$action' after '$previousAction'."
        }
        $previousSequence = $sequence
        $previousAction = $action
    }
}

$expectedNumericVersion = $ExpectedVersion.Split('-')[0]
$actualNumericVersion = Get-MsiProperty 'ProductVersion'
if ($actualNumericVersion -ne $expectedNumericVersion) {
    throw "MSI version '$actualNumericVersion' does not match expected '$expectedNumericVersion'."
}
if ((Get-MsiProperty 'ALLUSERS') -ne '2' -or (Get-MsiProperty 'MSIINSTALLPERUSER') -ne '1') {
    throw 'The MSI must support both scopes and default to per-user installation.'
}
if ((Get-MsiProperty 'UpgradeCode') -ne '{9E24290F-26A2-4CB8-A23B-D802E5C2CCFA}') {
    throw 'The MSI upgrade family has changed.'
}
$productCode = Get-MsiProperty 'ProductCode'
Assert-MsiActionOrder 'InstallUISequence' @(
    'AppSearch', 'SetINSTALLFORMACHINE', 'SetADDTOPATH', 'InstallOptionsDlg',
    'FindRelatedProducts', 'LaunchConditions', 'CostInitialize', 'CostFinalize',
    'MigrateFeatureStates', 'ExecuteAction'
)
Assert-MsiActionOrder 'InstallExecuteSequence' @(
    'FindRelatedProducts', 'LaunchConditions', 'CostInitialize', 'CostFinalize',
    'MigrateFeatureStates', 'InstallInitialize', 'RemoveExistingProducts', 'InstallFinalize'
)
$database = $null
$installer = $null
Write-Host "MSI metadata verified: $ExpectedVersion, $productCode."
Write-Host 'MSI upgrade detection, launch checks and removal ordering verified.'
if (-not $Install) {
    return
}

$registration = 'HKCU:\Software\Lantean Code\Roslyn Workbench MCP'
$machineRegistration = 'HKLM:\Software\Lantean Code\Roslyn Workbench MCP'
$installDirectory = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs\Roslyn Workbench MCP'
if ((Test-Path -LiteralPath $registration) -or (Test-Path -LiteralPath $machineRegistration) -or (Test-Path -LiteralPath $installDirectory)) {
    throw 'An installation or destination directory already exists. Use a clean Windows account; nothing was changed.'
}
$logDirectory = Join-Path ([IO.Path]::GetDirectoryName($MsiPath)) ('validation-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $logDirectory | Out-Null
$originalPath = [Environment]::GetEnvironmentVariable('PATH', 'User')
$installed = $false
try {
    Invoke-Msi @('/i', "`"$MsiPath`"", 'ALLUSERS=2', 'MSIINSTALLPERUSER=1', 'ADDLOCAL=Host') 'install'
    $installed = $true
    $executable = Join-Path $installDirectory 'roslyn-workbench-mcp.exe'
    $actualVersion = & $executable --version
    if ($LASTEXITCODE -ne 0 -or $actualVersion -ne $ExpectedVersion) {
        throw 'The installed command does not report the expected version.'
    }
    foreach ($file in @('LICENSE', 'THIRD-PARTY-NOTICES.md', 'Roslyn.Workbench.Mcp.pdb', 'Roslyn.Workbench.Mcp.runtimeconfig.json')) {
        if (-not (Test-Path -LiteralPath (Join-Path $installDirectory $file))) {
            throw "The installation is missing $file."
        }
    }
    if ([Environment]::GetEnvironmentVariable('PATH', 'User') -cne $originalPath) {
        throw 'The installation changed PATH without opting in.'
    }
    Invoke-Msi @('/fa', $productCode) 'repair'
    Write-Host 'Per-user installation, installed command, payload and repair verified.'
}
finally {
    if ($installed) {
        Invoke-Msi @('/x', $productCode) 'uninstall'
        if ((Test-Path -LiteralPath $registration) -or (Test-Path -LiteralPath (Join-Path $installDirectory 'roslyn-workbench-mcp.exe'))) {
            throw 'Uninstall left the application registration or executable behind.'
        }
        if ([Environment]::GetEnvironmentVariable('PATH', 'User') -cne $originalPath) {
            throw 'PATH differs from its original value after uninstall.'
        }
        Write-Host "Removal verified. Installer logs: $logDirectory"
    }
}
