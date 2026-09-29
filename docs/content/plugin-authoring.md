# Plugin authoring

Roslyn Workbench 1.0 publishes `Lantean.Roslyn.Workbench.Mcp.Plugins` as the supported compile-time package for trusted, in-process third-party plugins. A plugin project references this package only; it must not reference or deploy the Host, Workspace, CodeActions or bundled Core projects.

```shell
dotnet add package Lantean.Roslyn.Workbench.Mcp.Plugins --version VERSION
```

The package supplies the Plugins and Abstractions assemblies plus the C# authoring analyser. Query and mutation handlers compile against those contracts, while the installed Host supplies the matching runtime assembly identities and owns plugin loading, validation, execution leases, Workspace access and transaction staging. Do not copy the Plugins or Abstractions assemblies into the deployed plugin directory.

Plugins remain a manual installation for 1.0. Start the Host with `--enable-plugins` and one or more explicit `--plugin-directory` values only after reviewing and trusting the plugin code and its dependencies. The Host does not treat an in-process plugin as a hostile tenant.

The plugin API version is independent of the NuGet package version. A plugin declares an exact `PluginApiVersions` value, and the Host rejects unsupported API versions before publishing any of that plugin's tools.

See the [complete authoring guide](https://github.com/lantean-code/roslyn-workbench-mcp/blob/main/docs/PluginAuthoring.md) for entry-point, handler, result, transaction and deployment requirements, and [plugin authoring diagnostics](https://github.com/lantean-code/roslyn-workbench-mcp/blob/main/docs/PluginAuthoringDiagnostics.md) for analyser rules and remediation.
