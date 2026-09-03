# Windows MSI packaging

This WiX 7 project builds a framework-dependent Windows x64 installer. It appears under Packaging in `Roslyn.Workbench.Mcp.slnx`, with solution builds disabled: use the build script below to supply the published Host and installer inputs. Normal development builds do not build an MSI or calculate release versions. WiX's `wix7` EULA is accepted explicitly for this project's approved open-source use; reassess the applicable terms if its circumstances change.

## Build

To load the project in Visual Studio, install [HeatWave for Visual Studio](https://marketplace.visualstudio.com/items?itemName=FireGiant.FireGiantHeatWaveDev17). Restart Visual Studio after installation; if the project still displays as incompatible while the extension activates, reload the project or reopen the solution. Command-line builds and GitHub Actions use the NuGet SDK and do not need the extension.

Run from PowerShell 7 on Windows with the SDK pinned by `global.json`:

```powershell
./packaging/windows/build-msi.ps1
```

Local builds use `0.0.0-dev`. Release automation supplies the existing `RoslynWorkbench*` identity properties; this script never runs GitVersion. `-Version` can supply a specific version for installer testing, and `-OutputDirectory` selects a new, nonexistent output directory. The script preserves all previous build directories instead of deleting their contents.

The output contains the published Host, a single self-contained MSI container (not a self-contained .NET application), WiX debugging data and an adjacent SHA-256 checksum. The Host executable is named `roslyn-workbench-mcp.exe`; the DLL and assembly identities are unchanged. Runtime files, bundled plugins, third-party notices, licences and portable PDBs remain in the package.

The MSI is unsigned for now. Windows may warn about or block unsigned downloads, and managed devices may prohibit installation. Signing will be added separately: sign the project's binaries before building the MSI, sign the MSI afterwards, and regenerate its checksum. MSI publication through Microsoft Store still requires our signature; Store-managed signing is a separate MSIX distribution route.

## GitHub Actions

The manual **Build and publish release** workflow has an unchecked **Build MSI** (`build-msi`) option. It applies to every supported branch channel, including production tags; MSI builds are not automatic on `main`, pushes or pull requests.

When selected, the Windows release-validation job builds the installer with the same release version, commit and build-time Sentry configuration as the .NET tool package. It checks MSI metadata, installs without PATH changes, verifies the installed command and payload, repairs, then uninstalls on the disposable runner. A failed installer check stops publication. This smoke check complements the existing .NET-tool acceptance suite; it does not replace the additional installer scenarios listed below.

Successful builds retain the MSI and its adjacent checksum in the `roslyn-workbench-mcp-VERSION-win-x64-msi` workflow artefact for 14 days. Installer validation logs are uploaded separately. The published Host directory and WiX debugging data are not uploaded as release assets.

With **Publish** disabled, the MSI is available only as a workflow artefact. With **Publish** enabled, either package destination also attaches the same MSI and checksum to the draft GitHub Release after verifying the checksum. A maintainer must still publish that draft manually. The checkbox does not enable signing or change the selected NuGet feed.

## Installation

Interactive setup offers all-users installation and an optional PATH checkbox. Both are off by default. Per-user installation uses `%LOCALAPPDATA%\Programs\Roslyn Workbench MCP` without elevation; all-users installation uses Program Files and requires administrator approval. MSI's normal maintenance operations support repair and removal. Close MCP clients before upgrading or removing their running server.

Only the compatible .NET 10 x64 runtime is checked. Setup neither downloads nor installs prerequisites, and does not block installation because an SDK or MSBuild is absent. The server's status reports missing workspace tooling. Uninstallation remains available if the runtime has subsequently been removed.

Silent per-user installation without PATH changes:

```powershell
msiexec /i "roslyn-workbench-mcp-VERSION-win-x64.msi" /qn /norestart ALLUSERS=2 MSIINSTALLPERUSER=1 ADDLOCAL=Host
```

For all users, use `ALLUSERS=1` from an elevated session. To include PATH, use `ADDLOCAL=Host,Path` instead. For silent upgrades, omit `ADDLOCAL` to migrate the previous feature choice. `ADDTOPATH=1` also enables the feature for a fresh silent installation. PATH changes apply to the chosen installation context; restart terminals and clients afterwards.

The installer does not configure MCP clients, install a service, add startup entries or remove separately installed .NET tools. Do not install overlapping copies into the same directory. Use the same scope when upgrading; uninstall the previous MSI before changing scopes. If a .NET global tool already provides the command, choose the intended installation and configure an explicit executable path to avoid PATH ambiguity.

Only installer-owned files and registrations are removed. User state directories, repositories and additional source-built plugin files are not recursively deleted. Direct client configuration can use the absolute installed executable path when PATH integration is disabled.

## Versions and upgrades

MSI uses the numeric `Major.Minor.Patch` version. Filenames, Host metadata and installer comments retain the complete semantic version. Each built package receives new ProductCode and PackageCode identifiers; the UpgradeCode remains fixed. Repairs must use the original package, not a rebuild of the same version.

Same-numeric-version major upgrades are deliberate: a beta can replace another beta or production build with that numeric version. MSI does not compare prerelease labels. Numerically older versions are blocked. `ICE61` is suppressed specifically for this approved policy; the remaining Windows Installer validation checks are enabled. Removal of the previous version occurs within the installation transaction so a failed upgrade can roll back.

## Validate

Inspect metadata and the compiled UI/execute action ordering without installing:

```powershell
./packaging/windows/test-msi.ps1 -MsiPath "path/to/package.msi" -ExpectedVersion "0.0.0-dev"
```

Add `-Install` only on a suitable Windows account: it installs, checks the command and essential payload, repairs, then uninstalls. It refuses an existing MSI installation or destination directory and does not opt into PATH changes. Installer logs remain beside the MSI.

Before public distribution, also exercise the interactive dialogs, missing-runtime rejection, all-users elevation, optional PATH addition/removal, retained PATH choice during upgrades, same-version replacement, numerical downgrade rejection and rollback on an isolated Windows machine. Validate real MCP stdio calls and workspace loading from the installed executable. Do not mistake a successful MSI build for all of those installation checks.
