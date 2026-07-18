# Internal Argument-Null Guard Audit

## Scope

This audit covers production uses of:

- `throw new ArgumentNullException(...)` in internal DI-created constructors;
- `ArgumentNullException.ThrowIfNull(...)` on non-nullable parameters handled
  entirely inside the application.

It does not propose removing validation from public plugin contracts or other
APIs callable by code outside the application. It also does not affect content
or path validation where a non-null value may still be malformed or unsafe.

## Historical Baseline

The 2026-07-11 production scan found 73 argument-null guards across 38 files:

| Project | Total | Internal candidates | Retained public boundary |
|---|---:|---:|---:|
| `Roslyn.Workbench.Mcp` | 17 | 17 | 0 |
| `Roslyn.Workbench.Mcp.Workspace` | 28 | 27 | 1 |
| `Roslyn.Workbench.Mcp.CodeActions` | 10 | 10 | 0 |
| `Roslyn.Workbench.Mcp.Plugins` | 11 | 5 | 6 |
| `Roslyn.Workbench.Mcp.Plugins.Core` | 7 | 4 | 3 |
| **Total** | **73** | **63** | **10** |

The two explicit `throw new ArgumentNullException(...)` occurrences are both
constructor dependency guards in `AtomicFileWriter` and should be removed. Of
the 71 `ThrowIfNull` occurrences, 61 are internal candidates and 10 protect
public extension contracts.

The unit-test scan also found 42 direct `ArgumentNullException` assertions
across 20 files. They correspond to internal guard behaviour and should be
removed with the production branches rather than preserved as coverage.

## Completed Remediation

The 2026-07-11 remediation removed all 63 internal production guards and all 42
direct exception-only assertions. The remaining ten production guards are the
reviewed public plugin or Workspace contract boundaries listed below.

## Internal Remediation Inventory

### Host

All 17 Host guards are internal implementation checks:

| Implementation | Guards |
|---|---:|
| `McpPublishedResultSerializer` | 6 |
| `ToolSchemaBuilder` | 4 |
| `RoslynWorkbenchHostApplicationBuilderExtensions` | 2 |
| `WorkspaceToolResultMapper` | 2 |
| `ToolRequestBinder` | 1 |
| `ToolSchemaFactory` | 1 |
| `QueryResponseContractInspector` | 1 |

These methods are reached through Host composition or typed internal adapters.
Nullable warnings and controlled construction already prohibit null arguments.

### Workspace

Workspace has 27 internal candidates:

| Implementation family | Guards | Disposition |
|---|---:|---|
| `AtomicFileWriter` | 4 | Remove two constructor dependency exceptions and two internal content/encoding guards. |
| Workspace execution leases | 5 | Remove factory guards; callers construct the discriminated lease states internally. |
| `WorkspaceResolver` | 6 | Remove guards on Roslyn values and selectors supplied through typed internal flows. |
| `WorkspaceSessionStore` | 4 | Remove session and validation-delegate guards from the internal state store. |
| `WorkspaceSelectionResult` | 2 | Remove guards from internal success/failure factories; nullable-flow attributes retain state evidence. |
| `WorkspaceChangeDetector` | 1 | Remove the internal solution guard. |
| `WorkspaceSelectorService` | 1 | Remove the internal host-snapshot guard. |
| `WorkspaceStateTransitions` | 1 | Remove the internal session guard. |
| `SnapshotGuard` | 1 | Remove the internal session guard. |
| `TransactionCommitService` | 1 | Remove the internal selection guard. |
| `WorkspaceCommitLockAcquisition` | 1 | Remove the internal ownership guard while retaining its validated state factories. |

`SelectorResolveResult<T>.Resolved` remains a public Workspace contract factory,
so its single guard remains.

### Code Actions

All 10 guards are internal catalogue, handler, composition, mapping or
resolution checks. Remove them from registrations, base handlers,
`CodeActionToolRegistry`, `CodeActionWorkspaceResultMapper`,
`MefCodeActionProviderCatalog` and `CodeActionDescriptorRegistry`.

### Plugins

Remove five internal adapter guards from the two closed registrations,
`PluginWorkspaceResultMapper`, `PluginRegistry.GetRegisteredPluginTool` and the
private generic-type validator. Retain six guards at the public plugin boundary:

- two `ToolExecutionContextLease<TContext>` public factory guards;
- the public query and mutation handler arguments in `PluginRegistry`;
- public plugin and tool metadata validation in `PluginRegistry`.

### Plugins.Core

Remove four guards from the internal query/mutation base handlers and
`ToolExecutionHelpers`. Retain the two public `BoundedCollection<TItem>` factory
guards and the public `BundledCorePlugin.Register` boundary guard.

## Remediation Strategy

1. Remove the 63 internal production guards and their 42 direct exception
   assertions together, project by project.
2. Preserve nullable-flow attributes and discriminated-state factories; guard
   removal must not weaken successful/error state evidence.
3. Do not remove value validation such as empty identifiers, invalid paths,
   escaped artifact paths, invalid enum states or malformed persisted data.
4. Build after each project group, then run affected unit tests and the full
   suite.
5. Re-run the production and test scans and update this document to a completed
   baseline.

## Enforcement

`InternalArgumentNullGuardAuditTests` parses every C# file under `src` and fails
when an internal type contains `ArgumentNullException.ThrowIfNull(...)` or
constructs `ArgumentNullException`. Publicly callable contract types remain
outside that rule and retain their reviewed boundary validation. The test
guidance separately prohibits `null!`-driven tests for internal constructor and
method contracts. The enforcement test lives in the Host test project's
`Architecture` area and runs in the default fast loop; it is source governance,
not part of the Code Action compatibility audit.
