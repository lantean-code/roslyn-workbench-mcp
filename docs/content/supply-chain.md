# Supply-chain evidence

Roslyn Workbench is distributed as one NuGet package, `Lantean.Roslyn.Workbench.Mcp`. `dnx` and global .NET tool installation acquire the same `.nupkg`; MSI, MSIX, Debian and RPM packages are not produced or supported. Pin an exact package version in managed client configuration so acquisition and upgrades remain deliberate.

## Release evidence

Every validated release candidate contains the tool package, its symbol package, a release manifest, coverage summaries and SHA-256 checksums. The release workflow also produces a `supply-chain` directory containing:

- an SPDX 2.2 SBOM generated for the exact `.nupkg` and `.snupkg` bytes;
- the SBOM validator result;
- a sanitised dependency-vulnerability report covering every source project in the locked Host dependency graph;
- a compact evidence index relating the package ID and version to the source commit, workflow run, package hashes, SBOM hash and vulnerability result; and
- subject checksums used to create GitHub attestations for published packages.

The evidence assembler rejects missing or additional package payloads, retired installer formats, mismatched SBOM hashes, incomplete dependency inventories, missing project scans, malformed scan data, unknown severities and high or critical dependency findings. It cross-checks the complete package inventory against each committed source-project lock and checks every vulnerability finding against that inventory. Source-machine paths and raw scan inputs are not retained in published or failed-run evidence. Lower-severity findings remain visible for contextual review.

## Dependency lock

The shipped Host graph owns committed `packages.lock.json` files for the Host and each referenced source project. Release restoration starts at the Host in locked mode, so an undeclared dependency-graph change stops the release instead of silently selecting a new package. Maintainers update the graph deliberately with `dotnet restore src/Roslyn.Workbench.Mcp/Roslyn.Workbench.Mcp.csproj --use-lock-file --force-evaluate`, review every resulting lock change and run the normal validation before committing it.

## Trust boundaries

NuGet.org publication uses OIDC trusted publishing for the `Lantean` organisation. GitHub Packages publication uses the release job's scoped token. When publication is requested, GitHub artifact attestations bind build provenance and the validated SBOM to the exact package hashes. These mechanisms establish workflow and publication provenance; they are not an independent NuGet package-signing certificate and do not replace verification of the selected feed, version and repository.

Download the evidence files from the matching GitHub Release and check `checksums.sha256` before relying on them. GitHub's CLI can verify an available attestation against this repository, for example `gh attestation verify <package-path> --repo lantean-code/roslyn-workbench-mcp`.
