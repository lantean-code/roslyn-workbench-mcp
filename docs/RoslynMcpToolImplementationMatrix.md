# Roslyn MCP Server - Tool Implementation Matrix

## Purpose

This matrix records the planned implementation source for every public tool.
It prevents the catalogue from treating Roslyn as a single capability that
automatically supplies all refactorings. `Core` means a stable public Roslyn or
host API is sufficient. `Custom` means the plugin owns syntax/semantic logic.
`MEF` means an installed Roslyn code-fix or refactoring provider is required;
the tool is not enabled unless that provider is successfully composed and
validated.

## Server and Workspace Context

| Tool | Implementation source | Status |
|---|---|---|
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
|---|---|---|
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
|---|---|---|
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
|---|---|---|
| `move-type-to-file` | Custom syntax/semantic solution edit | Custom |
| `move-type-to-namespace` | Custom syntax/semantic solution edit | Custom |
| `rename-symbol` | Public `Renamer.RenameSymbolAsync` | Core |
| `extract-method` | MEF refactoring provider | MEF |
| `extract-variable` | MEF refactoring provider | MEF |
| `extract-constant` | MEF refactoring provider | MEF |
| `extract-interface` | MEF refactoring provider | MEF |
| `extract-base-class` | MEF refactoring provider | MEF |
| `introduce-parameter` | MEF refactoring provider | MEF |
| `inline-variable` | MEF refactoring provider | MEF |
| `change-signature` | MEF refactoring provider | MEF |
| `encapsulate-field` | MEF refactoring provider | MEF |
| `convert-to-async` | Custom syntax/semantic implementation | Custom |
| `convert-expression-body` | Custom syntax transformation | Custom |
| `convert-property` | Custom syntax/semantic transformation | Custom |
| `convert-foreach-linq` | Custom syntax/operation transformation | Custom |
| `convert-to-interpolated-string` | Custom syntax/semantic transformation | Custom |
| `convert-to-pattern-matching` | Custom syntax/semantic transformation | Custom |
| `generate-constructor` | Custom syntax/semantic generation | Custom |
| `generate-equals-hashcode` | MEF refactoring provider | MEF |
| `generate-overrides` | MEF refactoring provider | MEF |
| `generate-tostring` | Custom syntax/semantic generation | Custom |
| `implement-interface` | MEF code-fix/refactoring provider | MEF |
| `add-null-checks` | Custom syntax/semantic transformation | Custom |
| `add-missing-usings` | MEF code-fix provider | MEF |
| `remove-unused-usings` | MEF code-fix provider | MEF |
| `sort-usings` | Public formatter/syntax transformation | Core |
| `format-document` | Public `Formatter` APIs | Core |

## Code Actions and Transaction Control

| Tool | Implementation source | Status |
|---|---|---|
| `list-code-actions` | MEF-composed code-fix and refactoring providers | MEF |
| `stage-code-action` | Host action-token revalidation plus MEF provider | MEF |
| `stage-code-fix` | Host action-token revalidation plus MEF provider | MEF |
| `stage-fix-all` | Host action-token revalidation plus MEF Fix All provider | MEF |
| `transaction-start` | Host transaction coordinator | Core |
| `transaction-preview` | Host diff and revision journal | Core |
| `transaction-history` | Host bounded revision journal | Core |
| `transaction-commit` | Host durable commit journal and file writer | Core |
| `transaction-rollback` | Host transaction coordinator | Core |

## Provider Rule

An `MEF` tool is registered only when its provider and all required feature
assemblies are available, compose successfully, and pass the server's contract
tests. Failure disables that tool through normal plugin loading diagnostics; it
does not silently fall back to a different behaviour. The matrix must be
updated before a custom replacement is advertised.
