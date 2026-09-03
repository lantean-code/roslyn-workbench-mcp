# Distribution identities

This document records the product naming and package identities used or intended for each distribution target. An intended identity does not mean that a package has been published, a name has been reserved or a distribution has been validated. Release channels and publication procedures are covered by [Releasing](releasing.md); plugin package publication is covered separately by [Plugin distribution](plugin-distribution.md).

## Shared naming

| Purpose | Name |
| --- | --- |
| Product display name | `Roslyn Workbench MCP` |
| Publisher display name | `Lantean Code` |
| Executable command | `roslyn-workbench-mcp` (`roslyn-workbench-mcp.exe` on Windows) |
| Unqualified package name | `roslyn-workbench-mcp` |
| Repository name | `roslyn-workbench-mcp` |

Keep the product and command names consistent across distributions. Package identifiers follow the target ecosystem's rules and conventions; they do not need to be identical to the command or the .NET assembly name. The NuGet package uses the publisher-qualified `Lantean` prefix without changing the product, command, assembly or namespace identities.

## Distribution mapping

| Target | Package identity or name | Implementation status |
| --- | --- | --- |
| .NET tool through NuGet.org or GitHub Packages | `Lantean.Roslyn.Workbench.Mcp` | Publisher-qualified Host package identity; the feed does not change the package ID. Publication to a feed is separate from building the package. |
| Direct Windows MSI download | Display name `Roslyn Workbench MCP`; UpgradeCode `9E24290F-26A2-4CB8-A23B-D802E5C2CCFA` | [WiX packaging](../../packaging/windows/README.md) and opt-in release workflow implemented. Currently unsigned. ProductCode and PackageCode vary per build; UpgradeCode identifies the product family. |
| Windows MSIX development package | `LanteanCode.RoslynWorkbenchMcp.Development` | [MSIX packaging](../../packaging/windows/msix/README.md) and opt-in release workflow implemented. Unsigned development artefact; not an installable public distribution. |
| Microsoft Store | Use the exact package identity and publisher supplied by Partner Center | Deferred. The development MSIX identity is not a reserved Store identity. |
| WinGet | `LanteanCode.RoslynWorkbenchMcp` | Intended identity; manifest and submission not yet implemented. Intended to reference the existing MSI. |
| Chocolatey | `roslyn-workbench-mcp` | Intended identity; packaging and submission not yet implemented. |
| Debian/Ubuntu (`.deb`, APT) | `roslyn-workbench-mcp` | [Linux x64 packaging](../../packaging/linux/README.md) and opt-in release workflow implemented. Direct `.deb` downloads; no APT repository yet. |
| Fedora 44 (`.rpm`, DNF) | `roslyn-workbench-mcp` | [Linux x64 packaging](../../packaging/linux/README.md) and opt-in release workflow implemented. Direct `.rpm` downloads; no RPM repository. |
| Other RPM-based Linux distributions | `roslyn-workbench-mcp` | Package identity established but not advertised as compatible until each target is exercised. |

Unimplemented public package IDs still need availability and repository-policy checks before submission. A name or prefix accepted by one registry does not establish ownership in another.

## Target-specific formats

- NuGet IDs are case-insensitive and conventionally dotted. Keep `Lantean.Roslyn.Workbench.Mcp` for both package feeds; `Lantean` identifies the publisher while the installed command remains `roslyn-workbench-mcp`. See the [NuGet ID reference](https://learn.microsoft.com/en-us/nuget/reference/nuspec#id).
- WinGet uses a publisher-qualified `Publisher.Package` identifier. `LanteanCode.RoslynWorkbenchMcp` identifies the publisher without changing the displayed product name. See [WinGet manifests](https://learn.microsoft.com/en-us/windows/package-manager/package/manifest).
- Debian package names allow lowercase letters, digits, plus signs, hyphens and periods; they must start with an alphanumeric character and contain at least two characters. `roslyn-workbench-mcp` follows these rules. See [Debian package naming](https://www.debian.org/doc/debian-policy/ch-controlfields.html#source).
- RPM supports the selected hyphenated package name. Distribution-specific repository requirements still apply. See the [RPM specification format](https://rpm.org/docs/4.20.x/manual/spec.html).
- Chocolatey's naming guidance favours lowercase, hyphenated package IDs. See [Chocolatey package creation](https://docs.chocolatey.org/en-us/create/create-packages/).
- MSI uses GUID-based installation identities rather than a registry-style package name. MSIX has its own package identity, including publisher and version; Store distribution must use the Partner Center identity. Neither changes the user-facing product or command name.

## Artefacts, catalogues and publication

The `.nupkg`, `.msi` and `.msix` are package formats. NuGet.org, GitHub Packages and GitHub Releases are publication destinations; WinGet is a catalogue that points to an installer hosted elsewhere. The intended WinGet installer URL is a version-specific MSI asset on a public GitHub Release, with its exact SHA-256 hash recorded in the manifest. A draft release or temporary workflow artefact is not that public download location.

Building a `.deb` or RPM is separate from operating an APT or RPM repository. Direct downloads with tools such as `wget` or `curl` do not create another package identity or provide a package-manager update channel.

Keep versions and architecture out of the base package ID. Record them in package metadata and artefact filenames. Retain the MSI/MSIX version mappings documented with their packaging. Debian maps `0.1.0-beta.1` to `0.1.0~beta.1-1`; RPM maps it to version `0.1.0~beta.1` and release `1`. Both make a prerelease sort before production while the Host keeps its original SemVer.

The workflow's **Build MSI**, **Build MSIX**, **Build DEB** and **Build RPM** options are independent and unchecked by default. With **Publish** enabled, their outputs and checksums are attached to the draft GitHub Release; the maintainer publishes the draft manually. These options do not submit packages to WinGet, Chocolatey, Microsoft Store or Linux repositories, and do not enable signing.

## Decisions still needed before new targets ship

- WinGet: validate the manifest against a published MSI, decide installation scope, PATH and runtime-dependency behaviour, and verify silent installation, upgrade and removal before submitting. The proposed per-user default and automatic PATH integration for WinGet are not implemented by this naming document.
- Microsoft Store: obtain the real identity and settle runtime prerequisites, installed compatibility, versioning and submission. This target is currently on hold.
- Debian/Ubuntu: validate advertised target releases and establish signed repository hosting before offering updates through APT. Direct DEB installation is machine-wide with a .NET 10 runtime package dependency and no custom prerequisite download scripts.
- Fedora/RPM: validate any additional advertised distributions and establish signed repository hosting before offering package-manager updates. The direct Fedora 44 RPM is x86_64, depends on the package-managed .NET 10 runtime and adds no feeds or prerequisite scripts.
- Chocolatey: define packaging, dependencies, installation/removal behaviour and update hosting when work begins.

Signing and repository acceptance remain distribution-specific concerns. The current unsigned MSI and unsigned MSIX are not equivalent: the MSI can be installed subject to Windows and organisational policy, while the MSIX still needs an appropriate signing/trust or development-registration route. See the packaging documents for the remaining validation and signing work.
