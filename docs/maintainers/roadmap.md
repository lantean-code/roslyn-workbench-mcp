# Product direction

The .NET tool is the portable distribution route. There is no promised release cadence or formal observation period. Issues, Discussions, private security reports and consented diagnostic reports inform priorities. Release notes describe the capabilities available in each published version.

## Distribution direction

- Release the separate [`Lantean.Roslyn.Workbench.Mcp.Plugins` authoring package](plugin-distribution.md) alongside the Host, preserving its clean-consumer validation and the exclusion of Workspace implementation from authoring dependencies.
- Keep plugin installation manual. A curated plugin repository is outside the 1.0 product boundary; revisit it only when demand justifies a design for ownership, provenance, compatibility, reporting and removal. Listing must not imply a security endorsement.
- Prepare official MCP Registry publication under `io.github.lantean-code/roslyn-workbench-mcp`. Generate metadata from the exact NuGet package version using the then-current stable Registry schema, stdio transport and `dnx` runtime hint. Include the package ownership marker and approved icons. Registration must reuse the released package, not rebuild it. Package publication does not itself authorise Registry submission.
- Consider a small number of reputable client catalogues with maintainable ownership and update processes; avoid broad automated submissions.

Consult README and release notes for current contribution and plugin-package availability. Source-built plugins may run in the existing runtime. The current licence is MIT; any future change needs an explicit decision.

## Revisit only when needed

- Share read-only Workspace fixtures across test collections only if measured integration feedback time justifies it and thread safety is proven. Mutable state remains scenario-isolated.
- Replace source-governance tests with analysers only when the current check is materially too slow, too late or unreliable, and keep equivalent enforcement before removing the test.
- Remove the acceptance client's forced-cleanup fallback when a stable MCP SDK supports graceful redirected-stdin closure before waiting for process exit. Retain explicit stdin-EOF lifetime coverage.
- Support additional independently composed MEF module assemblies only when a concrete plugin needs them; define discovery, identity, isolation and collision rules first.

These are product directions and conditional design triggers, not dated delivery commitments or an implementation work log.
