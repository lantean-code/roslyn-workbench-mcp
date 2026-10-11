# Plugin packaging and deployment

Build plugins against the public authoring package, then deploy their output into a package directory. The Host discovers packages once at startup.

## Enable trusted packages

External plugin loading is disabled by default. The Host must be started with the valueless `--enable-plugins` switch before any configured plugin directory is accepted. This is an explicit operator decision to load reviewed packages as trusted in-process code; it does not apply to bundled first-party tools.

## Package layout

Each `--plugin-directory` value is a search root. Every immediate child directory is one package:

```text
plugins/
  example-tools/
    Example.Tools.dll
    Example.Tools.deps.json
    Example.PrivateDependency.dll
```

The marked entry assembly may have any file name. Other DLLs in the same package are dependencies and must not contain `RoslynPluginAttribute`. Discovery is not recursive, and DLLs directly beneath the search root are ignored.

## Shared and private assemblies

Host creates one non-collectible `AssemblyLoadContext` and `AssemblyDependencyResolver` per valid external package. Plugins, Abstractions, `System.Composition` and `Microsoft.CodeAnalysis*` identities are shared from the default context. Other managed and native dependencies resolve from the package, allowing separate plugins to carry different private dependency versions.

Do not deploy private copies of Plugins or Abstractions. The tutorial's `ExcludeAssets="runtime"` package reference prevents those contract assemblies being copied to the output. Keep private dependencies with the entry assembly; do not replace the Host's shared Roslyn or composition identities.

The bundled first-party tools are loaded with the Host rather than discovered from an external package directory. They use the same entry-point marker, validation and materialisation rules, and their tool names take precedence over external plugin names.

The entry assembly must have a valid informational semantic version. See [Microsoft's plugin tutorial](https://learn.microsoft.com/dotnet/core/tutorials/creating-app-with-plugin-support) for the underlying dependency-loading model.

## Names and collisions

Host validates package metadata and compatibility before executing plugin code. It composes the three Host-owned Code Action tools first and reserves those names together with other server-owned names. Collision rules are deterministic:

- reserved and Plugins.Core names defeat external plugins;
- every external package sharing a plugin ID is disabled;
- every external plugin sharing a tool name is disabled;
- no filesystem order chooses a winner.

## Diagnose a disabled plugin

`server-status` reports enabled plugins, warnings and disabled diagnostics without changing its existing JSON shape. Code Actions are not reported as plugins. Adding, removing or upgrading a package requires restarting the server.

Preparation diagnostics use stable IDs for handler contracts, lifetime, composition, instance state, mutable members, static state, legacy registration, tool metadata, behaviour and duplicate tool names. Host adds distinct discovery, plugin-metadata, collision and materialisation IDs. `PluginLoad` is reserved for an unexpected exception crossing the load boundary.

The authoring analyser provides early feedback, but runtime validation remains authoritative for metadata, handler shape, transport schemas and final names. See [Authoring diagnostics](diagnostics.md).

## Deployment checklist

- Target the same supported .NET runtime as the Host.
- Reference the public Plugins NuGet package for compilation. It includes the matching Abstractions assembly; do not package private copies of either assembly in the deployed plugin because Host supplies their runtime identity.
- Build with all default `RWMCP` diagnostics enabled and justify any deliberate suppression.
- Give the entry assembly a valid informational SemVer.
- Place exactly one marked entry assembly in one immediate package directory.
- Keep request and response contracts public and JSON serialisable.
- Derive query requests from `WorkspaceBoundRequest` and mutation requests from `WorkspaceMutationRequest`.
- Keep handlers thread-safe and non-disposable. Plugin singleton services must also be thread-safe, may implement `IDisposable` or `IAsyncDisposable`, and are released with the plugin service provider. Use constructor injection for plugin-owned dependencies.
- Validate names against server-owned, Code Action and Plugins.Core tools.
- Restart the stdio server and inspect `server-status` after deployment.
