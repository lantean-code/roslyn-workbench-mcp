# Distribution identity

Roslyn Workbench has one Host package identity and one supported package format. The NuGet package ID is `Lantean.Roslyn.Workbench.Mcp`, the installed tool command is `roslyn-workbench-mcp`, and the product name shown to people is **Roslyn Workbench MCP**.

The same `.nupkg` is published to NuGet.org or GitHub Packages according to the release channel. A feed is a publication destination and does not alter package identity. The package declares both `DotnetTool` and `McpServer` package types, so an exact version can be acquired transiently with `dnx` or installed as a global .NET tool. It embeds `.mcp/server.json` with the matching package identity, version and stdio transport; the manifest intentionally supplies no default Host arguments because inspection-only is already the safe runtime default.

MSI, MSIX, Debian, RPM, WinGet, Chocolatey and operating-system store packages are not built or supported. Reintroducing a second package format would require a new design covering identity, signing, repository trust, dependency handling, upgrade and removal, platform validation and evidence for the exact published bytes.

## Publication and trust

NuGet.org publication uses OIDC trusted publishing for the `Lantean` organisation. GitHub Packages publication uses the release job's scoped `GITHUB_TOKEN`. Published `.nupkg` and `.snupkg` hashes receive GitHub build-provenance and SBOM attestations. These controls authenticate their respective workflow and publication paths; they do not constitute an independent NuGet package signature.

Every release also includes checksums, a release manifest and the supply-chain evidence described in the [public documentation](../content/supply-chain.md). GitHub Releases retain copies for inspection, but package acquisition should use the selected authenticated NuGet feed rather than treating a draft release or temporary workflow artefact as another package channel.
