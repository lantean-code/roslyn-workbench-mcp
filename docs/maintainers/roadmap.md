# Product direction

The .NET tool is the portable distribution route. There is no promised release cadence or formal observation period. Issues, Discussions, private security reports and consented diagnostic reports inform priorities. Release notes describe the capabilities available in each published version.

## Distribution capabilities to prepare

- Prepare the separate [plugin package distribution workflow](plugin-distribution.md), including the authoring analyser and documentation. Validate the package through a clean external sample without repository project references, including query and mutation execution in the packaged Host. Workspace implementation must not become an authoring dependency.
- Establish a curated plugin repository with clear ownership, source/licence requirements, provenance, compatibility metadata, declared filesystem/process/network behaviour, vulnerability reporting and removal/update rules. Listing must not imply a security endorsement.
- Add managed installation from the same versioned Host output: MSI for Windows, Chocolatey and WinGet distribution, and Debian/Ubuntu packages via `apt`. Signing, repository trust, upgrade/removal and enterprise deployment guidance are part of those capabilities. Other package managers follow demonstrated demand.
- Prepare official MCP Registry publication under `io.github.lantean-code/roslyn-workbench-mcp`. Generate metadata from the exact NuGet package version using the then-current stable Registry schema, stdio transport and `dnx` runtime hint. Include the package ownership marker and approved icons. Registration must reuse the released package, not rebuild it. Package publication does not itself authorise Registry submission.
- Consider a small number of reputable client catalogues with maintainable ownership and update processes; avoid broad automated submissions.

Consult README and release notes for current contribution and plugin-package availability. Source-built plugins may run in the existing runtime. The current licence is MIT; any future change needs an explicit decision.

## Revisit only when needed

- Share read-only Workspace fixtures across test collections only if measured integration feedback time justifies it and thread safety is proven. Mutable state remains scenario-isolated.
- Replace source-governance tests with analysers only when the current check is materially too slow, too late or unreliable, and keep equivalent enforcement before removing the test.
- Remove the acceptance client's forced-cleanup fallback when a stable MCP SDK supports graceful redirected-stdin closure before waiting for process exit. Retain explicit stdin-EOF lifetime coverage.
- Support additional independently composed MEF module assemblies only when a concrete plugin needs them; define discovery, identity, isolation and collision rules first.

These are product directions and conditional design triggers, not dated delivery commitments or an implementation work log.
