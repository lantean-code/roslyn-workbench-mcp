# Linux packaging

This builds a framework-dependent Linux x64 Host in Debian and RPM packages named `roslyn-workbench-mcp`. They are intended for direct GitHub Release downloads on individually validated systems with a compatible .NET 10 runtime. They do not create APT/DNF repositories or submit anything to an official distribution archive. ARM64 is separate work.

## Build

Use Linux x64 with Python 3.11 or later and the .NET SDK pinned by `global.json`. DEB builds require `dpkg-deb` and `dpkg`; RPM builds require `rpmbuild`, `rpm`, `rpm2cpio` and `cpio`. No elevated access is needed to build or inspect either package. From the repository root:

```bash
python3 packaging/linux/build-deb.py
python3 packaging/linux/build-deb.py --version 0.1.0-beta.1
python3 packaging/linux/build-rpm.py
python3 packaging/linux/build-rpm.py --version 0.1.0-beta.1
```

The default version is `0.0.0-dev`; the script never invokes GitVersion. Release automation supplies the existing `RoslynWorkbench*` identity and optional build-time Sentry configuration. An explicit `--version` must agree with the environment's release identity. `--dotnet-path` selects another SDK executable.

Each build uses a new directory under `artifacts/debian` or `artifacts/rpm`, with the corresponding path under `/tmp/artifacts/roslyn-workbench-mcp` under WSL. `--output-directory` selects another new directory; existing directories are rejected. WSL SDK outputs also use the repository's temporary artifacts path. DEB staging and RPM construction use the Linux temporary filesystem so Windows-backed mounts cannot alter the archived Unix permissions. RPM builds also accept `--artifacts-path` to isolate SDK intermediates when a container mounts the source tree.

The output contains the published Host, package, adjacent SHA-256 checksum and validation logs. Each build checks metadata, ownership, permissions, the command symlink and byte-for-byte agreement with every published Host file, then starts the extracted Host to check its version and MCP initialisation/tool discovery. It does not install anything.

## Layout and prerequisites

| Path | Purpose |
| --- | --- |
| `/usr/lib/roslyn-workbench-mcp/` | Apphost, assemblies, bundled plugins, satellite assemblies, portable PDBs and licence notices. |
| `/usr/bin/roslyn-workbench-mcp` | Relative symlink to the apphost; no shell-profile or PATH edits are needed. |
| `/usr/share/doc/roslyn-workbench-mcp/` | Third-party notices and, in the DEB, copyright text and the compressed Debian packaging changelog. |
| `/usr/share/licenses/roslyn-workbench-mcp/` | RPM licence location. |

Files are root-owned and readable by ordinary users; only the apphost is executable. Installation is machine-wide and requires administrator authority through APT. The server runs as the user of its MCP client. There are no maintainer scripts, services, startup entries, client-configuration changes or repository registrations.

The DEB declares `Depends: dotnet-runtime-10.0 (>= 10.0.0)` and the RPM declares `Requires: dotnet-runtime-10.0 >= 10.0.0`. APT or DNF can install that runtime from already configured feeds. A package-managed .NET 10 SDK normally brings the runtime dependency with it; a manually extracted or user-local SDK does not satisfy the package manager's database. Neither package adds feeds or uses a custom prerequisite downloader. Follow the current .NET instructions for the selected distribution if the dependency is unavailable; do not mix runtime feeds indiscriminately.

Workspace operations still need a suitable SDK/MSBuild installation, whose availability the server reports. The package format does not establish compatibility with every Debian-derived release. Validate runtime dependency resolution and installed launch on each advertised target.

Initial installed validation covers Ubuntu 24.04 x64 for DEB and Fedora 44 x64 for RPM. It checks package-manager dependency resolution from an image without the SDK, unprivileged MCP initialisation/tool discovery, beta-to-production upgrade, reinstallation and removal. Other releases and RPM distributions require their own installed checks before being advertised as validated targets. These packaging smoke checks do not establish workspace-operation coverage on a runtime-only machine.

## Versions

The Host keeps its complete semantic version. Debian metadata replaces the first prerelease separator with `~` and appends packaging revision `-1`:

| Host version | Debian version |
| --- | --- |
| `0.1.0-beta.1` | `0.1.0~beta.1-1` |
| `0.1.0-rc.1` | `0.1.0~rc.1-1` |
| `0.1.0` | `0.1.0-1` |

This preserves the project's alpha/beta/RC/production ordering. Arbitrary SemVer prerelease labels are not guaranteed identical ordering under Debian's comparison algorithm. Build metadata is not accepted as the public version. With fixed packaging revision `-1`, a package-only correction needs a new release version; never replace a published asset with different bytes.

RPM metadata uses the same tilde mapping and keeps packaging revision `1` in its separate `Release` field:

| Host version | RPM version-release |
| --- | --- |
| `0.1.0-beta.1` | `0.1.0~beta.1-1` |
| `0.1.0-rc.1` | `0.1.0~rc.1-1` |
| `0.1.0` | `0.1.0-1` |

The tilde gives prereleases the required ordering under RPM version comparison. The package filename uses architecture `x86_64`. A package-only correction still needs a new product release version rather than replacing an existing public asset.

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

The equivalent direct RPM flow on Fedora 44 is:

```bash
sha256sum --check roslyn-workbench-mcp-0.1.0~beta.1-1.x86_64.rpm.sha256
sudo dnf install ./roslyn-workbench-mcp-0.1.0~beta.1-1.x86_64.rpm
sudo dnf remove roslyn-workbench-mcp
```

Install a newer downloaded RPM with the same DNF command to upgrade. Without a configured RPM repository, `dnf upgrade` cannot discover later GitHub downloads. The checksum is not a package signature, and the current direct-download RPM is unsigned.

## Validate

Inspect and launch an extracted package without installation:

```bash
python3 packaging/linux/test-deb.py \
  --package path/to/roslyn-workbench-mcp_0.1.0~beta.1-1_amd64.deb \
  --expected-version 0.1.0-beta.1

python3 packaging/linux/test-rpm.py \
  --package path/to/roslyn-workbench-mcp-0.1.0~beta.1-1.x86_64.rpm \
  --expected-version 0.1.0-beta.1
```

Supply `--publish-directory` to compare every packaged Host file with its original. Logs remain beside the package. The MCP smoke check uses an isolated owner-only state directory, disables error-report submission and does not open a workspace.

On a disposable matching Linux VM/container only, add `--install` and run with `sudo`. The check refuses an existing package or conflicting command/application directory, installs through APT or DNF, launches the server without root privileges, reinstalls and removes the package. It does not run `autoremove` or remove runtime prerequisites. Configured feeds and package indexes must resolve the runtime dependency; update them beforehand if needed.

The installed-file check expects the complete package inventory. Minimal images such as Ubuntu Base may configure `dpkg` to omit documentation; disable that size-saving configuration only in the disposable test image before running the full inventory check. A deliberately omitted file is reported by name rather than silently skipped.

For an upgrade check, also supply `--upgrade-from path/to/older.deb` or the corresponding older RPM. Both packages must be trusted locally built or released Roslyn Workbench packages. The baseline must have a lower package-manager version; it is installed before the selected package. Do not run installation mode on a workstation with an installation you want to preserve.

## GitHub Actions

The manual release workflow has independent, unchecked **Build DEB** (`build-deb`) and **Build RPM** (`build-rpm`) options. The Linux job uses the same release identity and Sentry build configuration. DEB validation runs on the disposable Ubuntu hosted runner. RPM construction and installed validation run in separate containers pinned to the same Fedora 44 image digest. This runs after, and does not alter, the existing .NET-tool acceptance checks.

Successful package and checksum files are retained for 14 days in format-specific artefacts. Validation logs upload separately, including on failure. **Publish** additionally verifies downloaded checksums and attaches selected Linux packages to the draft GitHub Release for either NuGet destination. A maintainer publishes the draft manually. Ordinary pushes and PRs do not publish packages.

The hosted checks validate their Ubuntu and Fedora images, not all Linux releases. An upgrade with a distinct earlier package is separate from the workflow's same-version reinstallation checks.
