# Roslyn Workbench plugin authoring

Add trusted, in-process query and mutation tools to Roslyn Workbench with `Lantean.Roslyn.Workbench.Mcp.Plugins`. The package includes the Plugins API, matching Abstractions contracts and the authoring analyser; plugin projects do not need a separate Workspace package or repository project references.

```bash
dotnet add package Lantean.Roslyn.Workbench.Mcp.Plugins --version VERSION
```

Replace `VERSION` with the exact supported release you intend to target. The Host supplies the Plugins and Abstractions runtime identities; do not deploy private copies of those assemblies.

## Start here

The authoring guides are maintained in the versioned documentation site:

- [Your first plugin](https://lantean-code.github.io/roslyn-workbench-mcp/latest/plugin-authoring.html): create, build and verify a query tool.
- [Mutation plugins](https://lantean-code.github.io/roslyn-workbench-mcp/latest/plugins/mutations.html): return source-change candidates through the transaction boundary.
- [Contracts and services](https://lantean-code.github.io/roslyn-workbench-mcp/latest/plugins/contracts.html): validation, bounded results, service lifetimes and caching.
- [Packaging and deployment](https://lantean-code.github.io/roslyn-workbench-mcp/latest/plugins/deployment.html): package layout, shared assemblies and startup diagnostics.
- [Authoring diagnostics](https://lantean-code.github.io/roslyn-workbench-mcp/latest/plugins/diagnostics.html): `RWMCP001`–`RWMCP023` and their remediation.

Select the documentation version matching the Host you deploy against. Plugin API V1 is supported throughout Host 1.x; a plugin's own package version does not need to match the Host version.

## Trust and scope

Plugins execute with the Host user's operating-system permissions and are not sandboxed. Enable only fully trusted packages with `--enable-plugins` and `--plugin-directory`. Plugin discovery happens once at startup, so installation or upgrades require restarting the Host.

Plugins may add ordinary query and mutation tools. They cannot register Code Actions, replace lifecycle tools, import Host services or change the catalogue after startup. Queries are read-only; mutations return a `MutationCandidate` for Host validation and staging. Never write source files directly or mutate the associated Roslyn Workspace.
