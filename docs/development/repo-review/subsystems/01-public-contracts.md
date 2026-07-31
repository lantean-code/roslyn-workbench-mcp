# Subsystem review: public contracts and selector semantics

## Scope and relationships

This unit covers `Roslyn.Workbench.Mcp.Abstractions`, including workspace selectors, snapshot preconditions, result envelopes, resolver interfaces, query services and mutation candidate contracts. It is the dependency root for Workspace and the public Plugins SDK; Host, bundled plugins and Code Actions consume these contracts indirectly through those two layers.

## Implementation and boundary review

- Project references preserve the intended direction: Abstractions has no reference to Workspace, Plugins, CodeActions or Host implementation assemblies.
- Public selectors keep identity, document, project, symbol, location and scope choices explicit. `WorkspaceContractValidator` rejects ambiguous outer shapes, while resolver implementations perform semantic validation such as missing documents, unsupported nested selectors and stale snapshots.
- `WorkspaceSnapshotIdentity` and `SnapshotPrecondition` carry workspace ID, epoch, solution/transaction identity and revision information without exposing mutable Roslyn objects. Cross-project consumers consistently compare workspace instance before revision-specific state.
- `BoundedCollection<T>` communicates truncation separately from item data. Plugin response validation rejects raw/unbounded collection contracts and the bundled response models use bounded collections for result sets.
- `WorkspaceMutationCandidate` proposes a Roslyn `Solution`; it does not authorise writes. Workspace-owned validation, transaction staging and commit remain the only source mutation path.
- Public enums that cross JSON boundaries use explicit string converters where required. Internal Code Action enums intentionally follow the Host serializer/schema pair and did not reveal a schema/runtime mismatch.

## Consumers, DI and configuration

Abstractions types are consumed by `Roslyn.Workbench.Mcp.Workspace`, `Roslyn.Workbench.Mcp.Plugins`, `Roslyn.Workbench.Mcp.Plugins.Core` and their tests. They are data/service contracts rather than DI registrations and declare no configuration.

## Tests and conclusion

Contract validators, selector states, bounded collections and workspace result invariants have focused unit coverage in Workspace, Plugins and Host test projects. Cross-project use was revisited through plugin execution and Host binding. No validated finding originated in this unit.
