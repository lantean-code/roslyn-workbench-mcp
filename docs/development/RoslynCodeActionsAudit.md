# Roslyn Code Actions Audit

> **Completeness reassessment (2026-07-26):** This document records the historical Stage 7 decision for the source folders audited at that time. [RoslynCodeActionAvailabilityAudit-2026-07-26.md](RoslynCodeActionAvailabilityAudit-2026-07-26.md) supersedes its repository-wide completeness claim after finding language-neutral C# providers outside that scope and distinguishing replayable, mixed, custom and deliberately excluded families. The production ledger remains authoritative for current visibility.

## Purpose

This document records the final Stage 7 state for Roslyn MEF code actions.

It defines:

1. the production visibility rule
2. the authoritative source of truth
3. the final supported versus hidden classification for built-in C# families

This audit is specifically about built-in Roslyn MEF refactoring/code-fix families under:

- `ref/roslyn/src/Features/CSharp`
- `ref/roslyn/src/EditorFeatures/CSharp`

## Classification Rules

### Visibility rule

A built-in Roslyn family is visible only when the production ledger marks it supported. Unlisted or unsupported families stay hidden by default.

The ledger is authoritative for:

- `list-code-actions`
- `describe-code-action`
- `stage-code-action`
- `stage-code-fix`
- dedicated replay-wrapper visibility

### Support rule

A family is considered implemented only when all of these are true:

1. Roslyn actually offers the action in a controlled fixture.
2. The action can be matched deterministically.
3. Replay/staging succeeds against real workspace mutations.
4. The public MCP surface exposes it intentionally through the ledger.

### Impossible-under-current-rules rule

A family is hidden as impossible under current rules when support would require one of:

- `CodeActionWithOptions` or dialog-owned option gathering
- internal-only Roslyn services
- opaque Roslyn option payloads
- host-only state not expressible through the MCP contract
- IDE-only diagnostics not available through the server’s public/project-analyzer diagnostics path
- external Copilot/package/reference flows outside deterministic local replay

## Current Repository State

The Stage 7 backlog is closed.

Production now has:

- one authoritative built-in family ledger
- one executable compatibility matrix for supported providers
- hidden-by-default catalogue behaviour

Every built-in C# Roslyn MEF refactoring/code-fix family in the checked-out source has a final production state.

## Validated Support

The ledger contains the earlier replay-backed families plus the final completion-wave promotions below.

### Final completion-wave support

| Roslyn family | Execution mode | Notes |
| --- | --- | --- |
| `AddFileBanner` | Replay | Validated with sibling-banner discovery and exact `Add file header` title. |
| `EnableNullable` | Replay | Validated with nullable-disabled project fixture. |
| `SyncNamespace` | Replay | Validated with mismatched folder/namespace fixture. |
| `ConvertNamespace` | Replay | Validated with block-scoped namespace style fixture. |
| `ConvertToProgramMain` refactoring | Replay | Validated in console top-level project shape. |
| `ConvertToTopLevelStatements` refactoring | Replay | Validated in console `Program.Main` project shape. |
| `ConvertToExtension` | Replay | Validated after selector correction and ledger promotion. |
| `ConvertToRawString` | Replay | Validated with deterministic title and fixture. |
| `AddParameterCheck` | Replay | Validated with constructor-parameter null-check fixture. |
| `InitializeMemberFromPrimaryConstructorParameter` | Replay | Validated with primary-constructor initialisation fixture. |
| `Wrapping` | Replay | Validated after allowing Roslyn’s wrapping MRU auxiliary operation alongside `ApplyChangesOperation`. |
| `FullyQualify` code fix | Replay | Validated against unresolved `CancellationToken`. |
| `ConvertToProgramMain` code fix | Replay | Validated with analyzer-backed top-level-statement preference diagnostics. |
| `ConvertToTopLevelStatements` code fix | Replay | Validated with analyzer-backed `Program.Main` preference diagnostics. |
| `RemoveUnusedVariable` code fix | Replay | Validated against unused-local compiler diagnostics. |
| `SpellCheck` code fix | Replay | Validated against misspelt member-access suggestions. |

### Earlier validated replay support

Earlier Stage 7 waves already validated:

- `ExtractMethod`
- `IntroduceParameter`
- `InlineTemporary`
- `EncapsulateField`
- `ConvertForEachToLinqQuery` / `ConvertLinqQueryToForEach`
- `IntroduceVariable` / `IntroduceLocalForExpression`
- `ConvertToInterpolatedString`
- `AddImport` refactoring
- `AddImport` code-fix missing-using branch
- `ConvertAnonymousTypeToClass`
- `ConvertAnonymousTypeToTuple`
- `ConvertAutoPropertyToFullProperty`
- `ConvertToRecord`
- `ConvertDirectCastToTryCast`
- `ConvertLocalFunctionToMethod`
- `ConvertPrimaryToRegularConstructor`
- `ConvertTryCastToDirectCast`
- `InvertConditional`
- `InvertIf`
- `InvertLogical`
- `MoveDeclarationNearReference`
- `NameTupleElement`
- `ReplaceDocCommentTextWithTag`
- `AddAwait`
- `AddDebuggerDisplay`
- `ConvertIfToSwitch`
- `IntroduceUsingStatement`
- `ReplaceConditionalWithStatements`
- `UseExplicitType`
- `UseImplicitType`
- `UseNamedArguments`
- `UseRecursivePatterns`
- `ConvertBetweenRegularAndVerbatimString`
- `ConvertBetweenRegularAndVerbatimInterpolatedString`
- `ConvertForEachToFor`
- `ConvertForToForEach`
- `MakeLocalFunctionStatic`
- `ReverseForStatement`
- `ConvertNumericLiteral`
- `ConvertTupleToStruct`
- `ImplementInterfaceExplicitly`
- `ImplementInterfaceImplicitly`
- `InitializeMemberFromParameter`
- `InlineMethod`
- `MergeConsecutiveIfStatements`
- `MergeNestedIfStatements`
- `SplitIntoConsecutiveIfStatements`
- `SplitIntoNestedIfStatements`
- `UseExpressionBody`
- `UseExpressionBodyForLambda`
- `remove-unused-usings`

## Hidden Families

These families remain hidden because the current server model cannot support them without violating the rules above.

| Roslyn family | Hide reason |
| --- | --- |
| `AddMissingImports` | Requires paste-tracking host state and internal services instead of deterministic location replay. |
| `ChangeSignature` | `CodeActionWithOptions` plus internal change-signature services. |
| `ExtractInterface` | `CodeActionWithOptions` plus internal extract-interface services/options service. |
| `ExtractClass` | `CodeActionWithOptions` plus internal extract-class options service. |
| `GenerateConstructorFromMembers` | Dialog-backed member-pick flow. |
| `GenerateEqualsAndGetHashCodeFromMembers` | Dialog-backed generation flow. |
| `GenerateOverrides` | Dialog-backed pick-members service. |
| `MoveStaticMembers` | `CodeActionWithOptions`. |
| `PullMemberUp` | `CodeActionWithOptions`. |
| `AddMissingReference` code fix | External reference-resolution flow outside deterministic local replay. |
| `AddPackage` code fix | Package-install flow outside deterministic local replay. |
| `GenerateType` code fix | `CodeActionWithOptions`. |
| `JsonDetection` code fix | Depends on IDE-only diagnostics not available through the server’s public/project-analyzer diagnostics path. |
| `SimplifyThisOrMe` code fix | Depends on IDE-only diagnostics not available through the server’s public/project-analyzer diagnostics path. |
| `SimplifyTypeNames` code fix | Depends on IDE-only diagnostics not available through the server’s public/project-analyzer diagnostics path. |
| `UsePatternMatchingIsAndCastCheckWithoutName` code fix | Depends on IDE-only diagnostics not available through the server’s public/project-analyzer diagnostics path. |
| `CopilotImplementNotImplementedException` code fix | External Copilot-dependent flow. |
| `CopilotSuggestions` code fix | External Copilot-dependent flow. |

## Historical Completion Status

Stage 7 was complete for the C#-specific source scope recorded above:

- every provider found by that audit had a production state
- the ledger is authoritative for discovery and wrapper visibility
- the compatibility matrix protects supported providers against Roslyn changes

The 2026-07-26 reassessment found that this scope did not include language-neutral Core providers exported for C#. Broad provider inventory must therefore include the actual composed runtime providers, not only C#-specific source folders. Any resulting decision must update the ledger with an explicit disposition.

Ad-hoc source-vs-ledger regex scripts can still report parse artefacts, so the corrected maintenance check must enumerate composed provider types and distinguish runtime Features providers from editor-host-only providers.
