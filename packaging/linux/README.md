# Linux Debian packaging

This builds a framework-dependent Linux x64 Host in an `amd64` Debian package named `roslyn-workbench-mcp`. It is intended for direct GitHub Release downloads on Debian/Ubuntu systems with a compatible .NET 10 runtime. It does not create an APT repository or submit anything to an official distribution archive. ARM64 and RPM are separate work.

## Build

Use Linux x64 with Python 3.11 or later, `dpkg-deb`, `dpkg` and the .NET SDK pinned by `global.json`. No elevated access is needed to build or inspect the package. From the repository root:

```bash
python3 packaging/linux/build-deb.py
python3 packaging/linux/build-deb.py --version 0.1.0-beta.1
```

The default version is `0.0.0-dev`; the script never invokes GitVersion. Release automation supplies the existing `RoslynWorkbench*` identity and optional build-time Sentry configuration. An explicit `--version` must agree with the environment's release identity. `--dotnet-path` selects another SDK executable.

Each build uses a new directory under `artifacts/debian`, or `/tmp/artifacts/roslyn-workbench-mcp/debian` under WSL. `--output-directory` selects another new directory; existing directories are rejected. WSL SDK outputs also use the repository's temporary artifacts path. Staging always uses the Linux temporary filesystem so Windows-backed mounts cannot alter the archived Unix permissions.

The output contains the published Host, `.deb`, adjacent SHA-256 checksum and validation logs. The build checks metadata, ownership, permissions, the command symlink and byte-for-byte agreement with every published Host file, then starts the extracted Host to check its version and MCP initialisation/tool discovery. It does not install anything.

## Layout and prerequisites

| Path | Purpose |
| --- | --- |
| `/usr/lib/roslyn-workbench-mcp/` | Apphost, assemblies, bundled plugins, satellite assemblies, portable PDBs and licence notices. |
| `/usr/bin/roslyn-workbench-mcp` | Relative symlink to the apphost; no shell-profile or PATH edits are needed. |
| `/usr/share/doc/roslyn-workbench-mcp/` | Copyright/licence text, third-party notices and compressed Debian packaging changelog. |

Files are root-owned and readable by ordinary users; only the apphost is executable. Installation is machine-wide and requires administrator authority through APT. The server runs as the user of its MCP client. There are no maintainer scripts, services, startup entries, client-configuration changes or repository registrations.

The package declares `Depends: dotnet-runtime-10.0 (>= 10.0.0)`. APT can install that runtime from already configured feeds. A package-managed .NET 10 SDK normally brings the runtime dependency with it; a manually extracted or user-local SDK does not satisfy APT's database. The package does not add feeds or use a custom prerequisite downloader. Follow the current [Debian](https://learn.microsoft.com/dotnet/core/install/linux-debian) or [Ubuntu](https://learn.microsoft.com/dotnet/core/install/linux-ubuntu) .NET guidance if the dependency is unavailable; do not mix runtime feeds indiscriminately.

Workspace operations still need a suitable SDK/MSBuild installation, whose availability the server reports. The package format does not establish compatibility with every Debian-derived release. Validate runtime dependency resolution and installed launch on each advertised target.

Initial installed validation covers Ubuntu 24.04 x64 with the Ubuntu-packaged .NET 10 runtime and no SDK: APT dependency resolution, unprivileged MCP initialisation/tool discovery, beta-to-production upgrade, reinstallation and removal. Debian and other Ubuntu releases require their own installed checks before being advertised as validated targets. This packaging smoke check does not establish workspace-operation coverage on a runtime-only machine.

## Versions

The Host keeps its complete semantic version. Debian metadata replaces the first prerelease separator with `~` and appends packaging revision `-1`:

| Host version | Debian version |
| --- | --- |
| `0.1.0-beta.1` | `0.1.0~beta.1-1` |
| `0.1.0-rc.1` | `0.1.0~rc.1-1` |
| `0.1.0` | `0.1.0-1` |

This preserves the project's alpha/beta/RC/production ordering. Arbitrary SemVer prerelease labels are not guaranteed identical ordering under Debian's comparison algorithm. Build metadata is not accepted as the public version. With fixed packaging revision `-1`, a package-only correction needs a new release version; never replace a published asset with different bytes.

## Install, upgrade and remove

Download the `.deb` and its adjacent checksum into the same directory. Replace the example version below with the selected release:

```bash
sha256sum --check roslyn-workbench-mcp_0.1.0~beta.1-1_amd64.deb.sha256
sudo apt install ./roslyn-workbench-mcp_0.1.0~beta.1-1_amd64.deb
```

APT displays the proposed package and dependency changes. Configure the MCP client to launch `/usr/bin/roslyn-workbench-mcp`, particularly if a separate .NET global tool supplies the same command. This package does not remove or modify that installation.

Close running clients before upgrades/removal. Install a newer downloaded `.deb` with the same command to upgrade. Without an APT repository, `apt upgrade` cannot discover future GitHub downloads. Downgrades remain a deliberate package-manager choice, not a custom installer policy.

```bash
sudo apt remove roslyn-workbench-mcp
```

Removal deletes package-owned files and the command symlink, not user state, repositories or externally added files. Runtime packages are managed independently. The adjacent checksum detects mismatched downloads; it is not a publisher signature. A future APT repository needs signed metadata and documented signing-key trust.

## Validate

Inspect and launch an extracted package without installation:

```bash
python3 packaging/linux/test-deb.py \
  --package path/to/roslyn-workbench-mcp_0.1.0~beta.1-1_amd64.deb \
  --expected-version 0.1.0-beta.1
```

Supply `--publish-directory` to compare every packaged Host file with its original. Logs remain beside the package. The MCP smoke check uses an isolated owner-only state directory, disables error-report submission and does not open a workspace.

On a disposable Linux VM/container only, add `--install` and run with `sudo`. The check refuses an existing package or conflicting command/application directory, installs through APT, launches the server without root privileges, reinstalls and removes the package. It does not run `autoremove` or remove runtime prerequisites. Configured feeds and package indexes must resolve the runtime dependency; update them beforehand if needed.

The installed-file check expects the complete package inventory. Minimal images such as Ubuntu Base may configure `dpkg` to omit documentation; disable that size-saving configuration only in the disposable test image before running the full inventory check. A deliberately omitted file is reported by name rather than silently skipped.

For an upgrade check, also supply `--upgrade-from path/to/older.deb`. Both packages must be trusted locally built or released Roslyn Workbench packages. The baseline must have a lower Debian version; it is installed before the selected package. Do not run installation mode on a workstation with an installation you want to preserve.

## GitHub Actions

The manual release workflow has an unchecked **Build DEB** (`build-deb`) option, independent of MSI/MSIX. The Linux job uses the same release identity and Sentry build configuration, then checks APT installation, reinstallation and removal on the disposable hosted runner. This runs after, and does not alter, the existing .NET-tool acceptance checks.

Successful `.deb` and checksum files are retained for 14 days in `roslyn-workbench-mcp-VERSION-linux-x64-deb`. Validation logs upload separately, including on failure. **Publish** additionally verifies the downloaded checksum and attaches both files to the draft GitHub Release for either selected NuGet feed. A maintainer publishes the draft manually. Ordinary pushes and PRs do not publish packages.

The hosted check validates its Ubuntu image, not all Linux releases. An upgrade with a distinct earlier package is separate from the workflow's same-version reinstallation check.
