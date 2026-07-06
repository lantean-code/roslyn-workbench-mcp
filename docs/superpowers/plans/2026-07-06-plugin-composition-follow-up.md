# Plugin Composition Follow-Up

## Summary

The current `Program` refactor moved the host toward proper DI, but plugin composition still needs a follow-up pass.

The intended end state is:

- `Program.cs` stays thin.
- MCP server setup is driven through DI.
- plugin discovery/composition is hidden behind a dedicated abstraction.
- `McpServerOptions` is configured from DI rather than from manually assembled tool lists.
- any later MEF-based plugin implementation fits behind the same boundary.

## Decisions Captured

### 1. Use `McpServerOptions` from the MCP SDK as the DI seam

`McpServerOptions` belongs to the MCP SDK, not the .NET SDK.

The preferred pattern is:

- register the plugin catalog in DI
- register `IConfigureOptions<McpServerOptions>`
- build `ToolCollection` there

This avoids manually constructing the full tool list in `Program.cs` or the host-builder extension.

### 2. Do not register a raw `IReadOnlyList<Assembly>` in DI

This shape is too weak because it:

- does not express intent
- can easily collide with other assembly lists
- leaks composition details instead of exposing a capability

If an assembly-based discovery step still exists, it should sit behind a dedicated abstraction such as:

```csharp
internal interface IPluginAssemblySource
{
    IReadOnlyList<Assembly> GetAssemblies();
}
```

### 3. Keep DI as the app composition model, even if plugins move to MEF

If plugin loading later uses MEF, that should not replace the app's DI container.

Preferred split:

- DI owns the host and service graph
- MEF owns plugin discovery/composition
- a DI service bridges MEF output into the app model

That bridge should expose an app-owned abstraction such as:

- `IPluginCatalogProvider`
- `PluginCatalogSnapshot`

The rest of the host should not depend directly on MEF container types.

### 4. Hosted startup services are for startup actions, not primary tool graph assembly

The `MsBuildRegistrationHostedService` is an appropriate place for startup work such as:

- MSBuild registration
- eager validation
- startup diagnostics

It is not the right place to be the primary owner of MCP tool registration if the tool graph must already exist when the server is built.

## Target Design

### Near-term DI shape

Introduce DI-owned plugin catalog composition along these lines:

1. Register a plugin catalog service or provider.
2. Register `PluginCatalogSnapshot` from that provider.
3. Register `IConfigureOptions<McpServerOptions>`.
4. In that configurator, build:
   - server status tool
   - plugin MCP tools
   - workspace lifecycle tools
   - transaction tools
5. Assign the final list to `McpServerOptions.ToolCollection`.

### Later MEF-compatible shape

If plugin composition moves to MEF, the host-facing shape should remain the same:

1. `IPluginCatalogProvider` is resolved from DI.
2. Its implementation uses MEF internally.
3. It returns `PluginCatalogSnapshot`.
4. `IConfigureOptions<McpServerOptions>` consumes that snapshot.

## Follow-Up Work Items

- replace manual `new PluginCatalogLoader()` from the host-builder extension
- introduce a named plugin discovery abstraction
- move plugin catalog materialisation fully behind DI
- move MCP tool collection assembly into `IConfigureOptions<McpServerOptions>`
- keep server-status/tool-count logic inside that options configuration path
- decide whether `PluginCatalogLoader` remains assembly-based or is replaced by a MEF-backed provider
- if MEF is adopted, ensure MEF is only an internal implementation detail of the plugin catalog provider

## Constraints To Preserve

- no `IServiceProvider` access from tool implementations
- no broad service-locator pattern
- no raw collection registrations where a named abstraction is required
- keep `Program.cs` thin
- keep host composition explicit, testable, and DI-driven
