# Compatibility policy

Roslyn Workbench versions use semantic versioning for the Host and its released packages. The guarantees below take effect at `1.0.0` and apply throughout the supported 1.x line. Prerelease builds do not carry the stable compatibility guarantee; review their release notes before upgrading.

Four versioned surfaces are independent. A Host package version is not an MCP protocol revision, a tool contract version, a recovery format version or a plugin API version. MCP protocol negotiation determines whether the client and Host can communicate; the policies below determine whether a caller, durable recovery record or plugin can continue to work across Host releases.

## MCP tools

The built-in tool catalogue follows an additive 1.x policy. Existing tool names remain available. Existing required input properties, their types and nullability, enum values and meanings, and output field types and meanings do not change incompatibly. A 1.x release may add a tool, add an optional input property, add an output field, clarify prose or extend an explicitly extensible value set where an older client can safely ignore the new value.

Clients should ignore unknown output fields, continue to honour JSON Schema requirements and rediscover `tools/list` whenever the Host restarts or is upgraded. They must not infer compatibility from property order. Tool descriptions and human-readable error messages may improve without constituting a contract change.

## Structured errors and continuations

Stable machine-readable error codes retain their meaning during 1.x. Human-readable messages may be clarified, localised or expanded, so clients should branch on the code rather than exact prose. A client receiving an unknown code should surface it and its message safely, avoid guessing a recovery action and allow the operator to inspect status.

`RequiredAction` values and their continuation kind and target tool or tools are compatibility contracts. Instruction prose may improve. Clients should follow the structured continuation and should not parse its sentence for control flow.

## Durable recovery state

Recovery owner records and manifests use a format version independently from the Host version. Format version 1 is the first supported 1.x recovery format. Every supported later 1.x Host must read and recover supported version 1 evidence. Persisted recovery-state and file-operation enum members have explicit numeric values; existing values must never be renumbered or reused, and any compatible addition must use a new explicit value. Optional JSON properties that older readers can safely ignore may be added without incrementing the format; any change that alters interpretation or required recovery semantics requires a new format version and an explicit reader.

An unknown or unsupported format is never interpreted as the current format. The Host reports `RecoveryVersionUnsupported`, retains the evidence without rewriting, restoring, completing or deleting it, and advises the operator to start the same or a newer compatible Roslyn Workbench version. A newer Host can therefore recover supported old evidence without requiring the interrupted transaction to be rolled back and recreated. Downgrading is safe only when the older Host supports every format present in the state directory; otherwise, restart the newer compatible Host to complete recovery.

Before changing Host versions, finish or roll back active transactions where possible, stop every Host instance and retain unresolved recovery evidence. Do not edit version fields manually.

## Plugin API

The public plugin contract is versioned separately through `PluginApiVersions.V1`. Host 1.x supports that API version. The public surfaces in `Roslyn.Workbench.Mcp.Plugins` and its bundled `Roslyn.Workbench.Mcp.Abstractions` assembly remain binary compatible during 1.x: existing public types and members are not removed or renamed, signatures and constants do not change incompatibly, enum meanings are retained, and required members are not added to interfaces or base contracts implemented by plugins.

Compatible additions may be made where an existing compiled plugin remains loadable and its established behaviour remains valid. Deprecation does not remove a V1 member before Host 2.0. A plugin declares the exact API family it targets with `PluginApiVersions.V1`; its own package version remains the plugin publisher's compatibility contract and need not match the Host package version.

## Release changes

Every release note should identify additions or intentional compatibility changes separately for MCP tools, errors and continuations, recovery formats and the plugin API. A necessary breaking change is reserved for the next major Host or plugin API version unless preserving the old behaviour would cause unsafe recovery or operation; an exceptional safety break must fail explicitly and be called out with migration guidance.


The [maintainer compatibility guide](https://github.com/lantean-code/roslyn-workbench-mcp/blob/develop/docs/maintainers/compatibility.md) records how catalogue baselines, recovery mappings and package API checks enforce these guarantees.
