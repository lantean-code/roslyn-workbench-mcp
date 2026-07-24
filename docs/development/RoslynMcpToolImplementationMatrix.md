# Roslyn MCP Server - Tool Implementation Matrix

> **Engineering record:** This matrix records implementation planning and historical capability decisions. It is not the release inventory for a running server. MCP `tools/list` is authoritative for the current process; see [Tool discovery](../ToolDiscovery.md).

## Purpose

This matrix records the planned implementation source for every public tool. It prevents the catalogue from treating Roslyn as a single capability that automatically supplies all refactorings. `Core` means a stable public Roslyn or host API is sufficient. `Custom` means the plugin owns syntax/semantic logic. `MEF` means an installed Roslyn code-fix or refactoring provider is required; the tool is not enabled unless that provider is successfully composed and validated.

## Server and Workspace Context

| Tool | Implementation source | Status |
| --- | --- | --- |
| `server-status` | Host diagnostics and plugin registry | Core |
| `workspace-open` | Host `MSBuildWorkspace` loader and SDK-style validation | Core |
| `workspace-close` | Host workspace/transaction coordinator | Core |
| `workspace-status` | Host lifecycle, input manifest and transaction coordinator | Core |
| `workspace-reload` | Host `MSBuildWorkspace` loader and input manifest rebuild | Core |
| `get-solution-structure` | Custom projection of Roslyn solution/project data | Custom |
| `get-project-details` | Custom projection of Roslyn project, compilation and analyzer data | Custom |
| `get-document-options` | Custom projection of document, parse and analyzer-config data | Custom |

## Semantic Inspection and Navigation

| Tool | Implementation source | Status |
| --- | --- | --- |
| `get-document-outline` | Custom syntax tree and semantic-model projection | Custom |
| `get-code-context` | Custom source-text and enclosing-symbol projection | Custom |
| `search-symbols` | Public `SymbolFinder` declaration search | Core |
| `resolve-symbol` | Public `SemanticModel` symbol binding | Core |
| `get-symbol-info` | Public `ISymbol` APIs | Core |
| `get-symbol-members` | Public `INamedTypeSymbol` and member APIs | Core |
| `get-symbol-attributes` | Public attribute-data APIs | Core |
| `go-to-definition` | Public `SymbolFinder` definition APIs | Core |
| `find-references` | Public `SymbolFinder.FindReferencesAsync` | Core |
| `find-callers` | Public `SymbolFinder` caller APIs | Core |
| `find-callees` | Custom `IOperation` and invocation traversal | Custom |
| `find-implementations` | Public `SymbolFinder` implementation APIs | Core |
| `find-overrides` | Custom symbol hierarchy traversal | Custom |
| `find-derived-types` | Public `SymbolFinder` derived-type APIs | Core |
| `get-type-hierarchy` | Public symbol relationships plus `SymbolFinder` | Core |
| `find-overloads` | Public member and signature APIs | Core |
| `get-partial-declarations` | Public declaring-syntax references | Core |
| `get-symbol-dependencies` | Custom operation and symbol traversal | Custom |
| `get-symbol-dependents` | Custom reference-index projection | Custom |

## Analysis and Architecture

| Tool | Implementation source | Status |
| --- | --- | --- |
| `get-diagnostics` | Public compilation and analyzer diagnostics | Core |
| `get-code-metrics` | Custom syntax/semantic metrics implementation | Custom |
| `analyze-control-flow` | Public `SemanticModel.AnalyzeControlFlow` | Core |
| `analyze-data-flow` | Public `SemanticModel.AnalyzeDataFlow` | Core |
| `get-operation-tree` | Public `SemanticModel.GetOperation` projection | Core |
| `get-control-flow-graph` | Public `ControlFlowGraph` projection | Core |
| `find-unused-symbols` | Custom reference and accessibility analysis | Custom |
| `find-duplicate-code` | Custom normalised syntax/operation comparison | Custom |
| `get-dependency-graph` | Custom graph built from public symbol/reference APIs | Custom |
| `find-dependency-cycles` | Custom graph-cycle algorithm over dependency graph | Custom |
| `get-change-impact` | Custom composition of references and hierarchy APIs | Custom |
| `get-api-surface` | Custom projection of public symbol APIs | Custom |
| `get-test-impact` | Custom test-convention and reference analysis | Custom |
| `analyze-nullability` | Public nullable diagnostics plus custom projection | Custom |
| `analyze-async` | Custom syntax and operation analysis | Custom |
| `analyze-disposables` | Custom operation and data-flow analysis | Custom |

## Specific Refactorings, Generation and Formatting

| Tool | Implementation source | Status |
| --- | --- | --- |
| `move-type-to-file` | Future host wrapper over Roslyn move-type support using the narrowed Roslyn-chosen own-file contract | Planned with narrowed contract |
| `move-type-to-namespace` | Roslyn move-to-namespace support still depends on internal service and options seams | Not planned with current public Roslyn APIs |
| `rename-symbol` | Public `Renamer.RenameSymbolAsync` | Core |
| `extract-method` | Host wrapper over deterministic MEF refactoring replay | Batch 3 |
| `introduce-variable` | Host wrapper over deterministic MEF refactoring replay with Roslyn leaf-action selection | Batch 3 |
| `extract-interface` | MEF refactoring provider plus options-service interaction | Not planned with current public Roslyn APIs |
| `extract-base-class` | MEF refactoring provider plus options-service interaction | Not planned with current public Roslyn APIs |
| `introduce-parameter` | Host wrapper over deterministic MEF refactoring replay with nested-action selection | Batch 3 |
| `inline-variable` | Host wrapper over deterministic MEF refactoring replay | Batch 3 |
| `change-signature` | MEF refactoring provider plus internal Roslyn feature service wrapper | Not planned with current public Roslyn APIs |
| `encapsulate-field` | Host wrapper over deterministic MEF refactoring replay | Batch 3 |
| `convert-to-async` | Public Roslyn surface does not expose the documented end-state synchronous-to-asynchronous conversion workflow | Not planned with current public Roslyn APIs |
| `convert-expression-body` | Host wrapper over deterministic MEF refactoring replay | Batch 4 |
| `convert-property` | Future narrowed or split contract around Roslyn-backed auto-property and full-property conversions; current build ships the dedicated auto-property-to-full-property split | Planned with narrowed contract |
| `convert-foreach-linq` | Host wrapper over deterministic MEF refactoring replay | Batch 3 |
| `convert-to-interpolated-string` | Host wrapper over deterministic MEF refactoring replay | Batch 3 |
| `convert-to-pattern-matching` | Roslyn fixes depend on diagnostics not surfaced through the server's current public compilation and analyzer diagnostics path | Not planned with current public Roslyn diagnostics path |
| `generate-constructor` | Dialog-backed Roslyn member-pick flow | Not planned with current public Roslyn APIs |
| `generate-equals-hashcode` | MEF refactoring provider plus internal Roslyn feature service wrapper | Not planned with current public Roslyn APIs |
| `generate-overrides` | MEF refactoring provider plus internal generation APIs | Not planned with current public Roslyn APIs |
| `generate-tostring` | No supported public Roslyn generation seam identified in the current build | Not planned with current public Roslyn APIs |
| `implement-interface` | MEF code-fix/refactoring provider plus internal Roslyn feature service wrapper | Not planned with current public Roslyn APIs |
| `add-null-checks` | Host wrapper over deterministic MEF refactoring replay | Batch 4 |
| `add-missing-usings` | Host wrapper over deterministic MEF code-fix selection | Batch 2 |
| `remove-unused-usings` | Host wrapper over deterministic MEF code-fix selection | Batch 2 |
| `sort-usings` | Public formatter/syntax transformation | Core |
| `format-document` | Public `Formatter` APIs | Core |

## Code Actions and Transaction Control

| Tool | Implementation source | Status |
| --- | --- | --- |
| `list-code-actions` | MEF-composed code-fix and refactoring providers plus descriptor classification | Batch 2 |
| `describe-code-action` | Host token revalidation plus descriptor classification | Batch 2 |
| `stage-code-action` | Host action-token revalidation plus replay-only MEF provider execution | Batch 2 |
| `stage-code-fix` | Host action-token revalidation plus MEF provider | MEF |
| `stage-fix-all` | Host action-token revalidation plus MEF Fix All provider | MEF |
| `transaction-start` | Host transaction coordinator | Core |
| `transaction-preview` | Host diff and revision journal | Core |
| `transaction-history` | Host bounded revision journal | Core |
| `transaction-commit` | Host durable commit journal and file writer | Core |
| `transaction-rollback` | Host transaction coordinator | Core |

## Stage 7 Batch 2 split

- Replay-backed or deterministic scoped-codefix wrappers that currently land: `add-missing-usings`, `remove-unused-usings`, `inline-variable`, `convert-to-interpolated-string`, `extract-method`, `introduce-parameter`, `encapsulate-field`, `convert-foreach-linq`, `introduce-variable`.
- `list-code-actions` now uses a closed audited allowlist for built-in Roslyn families. Any family that does not yet have an approved dedicated execution path or validated replay rule is hidden from discovery by default rather than surfaced optimistically.
- `stage-code-action` remains replay-only. Parameterised actions must describe themselves first and are rejected by generic replay.

## Roslyn-source backlog status

The Stage 7 Roslyn MEF catalogue audit is complete.

- The authoritative ledger now covers the built-in C# `ExportCodeRefactoringProvider` and `ExportCodeFixProvider` families from the checked-out Roslyn source.
- Final completion-wave replay promotions include: `AddFileBanner`, `EnableNullable`, `SyncNamespace`, `ConvertNamespace`, `ConvertToProgramMain` refactoring, `ConvertToTopLevelStatements` refactoring, `ConvertToExtension`, `ConvertToRawString`, `AddParameterCheck`, `InitializeMemberFromPrimaryConstructorParameter`, `Wrapping`, `FullyQualify` code fix, `ConvertToProgramMain` code fix, `ConvertToTopLevelStatements` code fix, `RemoveUnusedVariable` code fix, and `SpellCheck` code fix.
- Families that remain hidden are hidden intentionally in the ledger because they require `CodeActionWithOptions`, internal-only Roslyn services, external Copilot/package/reference flows, paste-tracking host state, or IDE-only diagnostics that are not available through the server’s public diagnostics path.
- There is no remaining built-in C# family backlog; refer to [RoslynCodeActionsAudit.md](./RoslynCodeActionsAudit.md) for the final classification rationale.

## Provider Rule

An `MEF` tool is registered only when its provider and all required feature assemblies are available, compose successfully, and pass the server's contract tests. Failure disables that tool through normal plugin loading diagnostics; it does not silently fall back to a different behaviour. The matrix must be updated before a custom replacement is advertised.
