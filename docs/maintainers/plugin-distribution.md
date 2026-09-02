# Plugin package distribution

The Host tool and plugin-authoring package are separate distribution products. This document scopes the additional release work; it does not enable publication or change current package availability. See README and release notes for that availability.

## Existing package boundary

`Roslyn.Workbench.Mcp.Plugins.csproj` already defines package metadata and includes the authoring reference as its README. Its packaging targets include the Abstractions assembly and authoring analyser. The release workflow currently packs, validates and publishes only the Host tool. Adding the plugin package therefore requires a separate implementation, not just removing a prerelease condition.

## Preparation scope

- Pack the plugin-authoring project with the release's calculated identity. Inspect the resulting dependency graph and contents: consumers must receive the public contracts and authoring diagnostics without taking a dependency on Workspace implementation or the executable Host.
- Verify package metadata, licence, README, source identity and symbols. Establish the package's compatibility policy with the Host before presenting it as a supported external API.
- Validate a clean consumer using only the resulting NuGet package, not repository project references. Exercise authoring diagnostics and both query and mutation plugins in the packaged Host through the normal transaction boundary.
- Extend release artefacts, checksums and provenance to cover the plugin package explicitly. Preserve the existing Host-only release path; building a plugin package must not silently publish it.
- Prepare a deliberate publication choice and matching NuGet trusted-publishing policy for the plugin package identity. Do not broaden the Host package's current publication policy as an incidental edit.
- Publish matching authoring documentation and installation instructions when the package is made available. A curated plugin repository, third-party contributions and additional installers are separate decisions.

The maintainer must approve the implementation and activation before this workflow publishes any plugin package. No registry submission, contribution opening or NuGet publication is authorised by preparing these files.
