# Roslyn Workbench Workspace Support Package

`Roslyn.Workbench.Mcp.Workspace` contains the workspace contracts and runtime support required by Roslyn Workbench and its public plugin API.

Plugin authors should install the author-facing package:

```bash
dotnet add package Roslyn.Workbench.Mcp.Plugins
```

That package brings in the matching Workspace version transitively. Do not add a second direct Workspace package reference and do not deploy private copies of the Workspace or Plugins assemblies inside a plugin package; the Host supplies their runtime identity.

The Workspace package is published separately so NuGet can resolve the public selectors, project-system metadata and snapshot contracts exposed through the Plugins API. It is not an independently hosted server or a general-purpose Roslyn workspace facade.

See the [plugin authoring guide](https://github.com/lantean-code/roslyn-workbench-mcp/blob/main/docs/PluginAuthoring.md) for the supported extension model.
