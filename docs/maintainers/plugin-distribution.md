# Plugin package distribution

The Host tool and plugin-authoring package are separate distribution products released from one calculated version and source identity. Preparing this release path does not authorise a NuGet publication or registry submission; those remain deliberate maintainer actions through the release workflow.

## Package boundary

The authoring product has the exact package identity `Lantean.Roslyn.Workbench.Mcp.Plugins`. It contains the Plugins and Abstractions assemblies, portable symbols and the plugin-authoring analyser. A plugin project references this package only; it does not acquire the executable Host, Workspace implementation, CodeActions implementation, bundled Core plugins or MCP protocol implementation as package or deployed plugin dependencies.

Plugins owns the public authoring contracts, registration builders and the minimal captured configuration required to describe a plugin. The Host owns discovery, loading, runtime preparation and validation, context implementations, execution leases, cache integration, Workspace mapping and mutation staging. Abstractions remains the shared public contract assembly used by Plugins and supplied by the Host at runtime.

## Release and validation boundary

- The release workflow packs the Host tool and authoring library with the same calculated release identity, while preserving their distinct package types and contents.
- Package integration tests inspect the authoring package metadata, files, dependency graph and assembly references. They also build a clean consumer that references only the packed package and verify analyser activation.
- Published-Host acceptance builds that clean package consumer, loads its query and mutation tools and exercises mutation through the normal transaction boundary.
- Release checksums, SPDX SBOMs, vulnerability evidence, provenance and attestations cover both `.nupkg` and both `.snupkg` subjects explicitly. Missing, additional or misidentified release subjects fail validation.
- NuGet.org trusted publishing requires separate exact-ID policies for `Lantean.Roslyn.Workbench.Mcp` and `Lantean.Roslyn.Workbench.Mcp.Plugins`; a wildcard or broadened Host policy is not acceptable.
- Public authoring documentation explains installation, the trust boundary and the Host-supplied runtime identities. A curated plugin repository and additional installers are separate decisions.

The maintainer must approve publication explicitly. Preparing, building or testing these files does not publish either package or submit a registry entry.
