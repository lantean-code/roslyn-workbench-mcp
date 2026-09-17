# Compiler-impact validation

## Purpose and scope

Compiler-impact validation is an independent, opt-in commit policy for managed environments that want a deterministic assurance that a transaction has not introduced compiler errors. It does not require an existing imperfect Workspace to become error-free, and it is not enabled implicitly by any operational mode.

The initial policy is `no-new-compiler-errors`. Analyser diagnostics, test impact and public-API compatibility are outside this feature because they have different cost, trust and compatibility characteristics.

## Configuration and publication

The Host accepts `--commit-validation <none|no-new-compiler-errors>` and the equivalent `ROSLYN_WORKBENCH_MCP_COMMIT_VALIDATION` environment variable. The command-line value may appear only once, overrides the environment value and is validated exactly and case-sensitively. The default is `none`. Invalid values fail startup. Compiler validation cannot be selected with `inspection-only` because that mode cannot create or commit a transaction.

When validation is disabled, the Host does not register its validation service, publish `transaction-validate`, include validation guidance in the server instructions or perform compiler-diagnostic work. `server-status` reports the effective non-sensitive policy in either case.

## Validation boundary

Validation is lazy. Workspace open, reload, transaction start, staging and preview do not establish or refresh a compiler baseline. An explicit `transaction-validate`, approval-required `transaction-review` or `transaction-commit` evaluates the immutable solution captured at transaction start and the current staged solution. Revision zero is a complete empty comparison, so enabling validation does not replace the established no-change commit outcome with an error.

The affected set contains every changed loaded project evaluation and each loaded evaluation that transitively depends on it. Each evaluation uses the target framework selected when that project was loaded. Roslyn Workbench does not claim coverage for target frameworks, projects or build configurations absent from the loaded Workspace.

The baseline and staged snapshot each use the existing bounded, solution-snapshot-scoped Workspace query cache. Repeated validation of an unchanged revision can therefore reuse compiler results. A new transaction revision, Workspace reload or new Workspace epoch naturally uses a different solution snapshot and cannot reuse a stale result.

## Diagnostic identity and comparison

Only unsuppressed effective compiler errors carrying Roslyn's compiler diagnostic tag participate. The `compiler-error-identity-v1` identity consists of:

- normalised project path, falling back to project name when no path is available;
- loaded target framework;
- diagnostic identifier;
- invariant diagnostic message; and
- normalised document path, or a stable non-source location kind.

Source span is deliberately excluded so that moving an unchanged error within the same document does not make it new. Comparison is a multiset subtraction rather than a count comparison, so adding a second occurrence of an otherwise identical error is still detected.

## Failure and recovery

Validation fails closed when an affected compilation is unavailable, a relevant load error exists, an affected project is absent from either snapshot or external Workspace inputs have changed. Results report elapsed duration, affected project evaluations, baseline and staged counts, a bounded set of introduced diagnostics, structured incomplete reasons and relevant limitations.

A failed gate writes no files and leaves the transaction active. Newly introduced errors must be corrected or restaged before validation is retried. Incomplete assurance cannot be recovered against the same captured baseline and loaded project evaluations: the continuation requires rollback, resolution of the reported load, compilation or external-change condition, Workspace reload and a new transaction.

Commit tools run the gate before requesting user approval so an invalid transaction does not consume an elicitation. Workspace commit then revalidates authoritatively immediately before its normal filesystem certification and persistence pipeline. Receipt generation also requires successful validation and records that success in the review; it does not create a receipt for a failing or incomplete transaction.

## Testing strategy

Unit coverage includes configuration precedence and rejection, operational-policy independence, conditional service and tool publication, stable multiset comparison, span movement, transitive dependants, incomplete compilations, load errors, external changes, tool result mapping, review projection and both Host and Workspace commit gates. Workspace integration coverage uses materialised projects and real Roslyn compilations to verify that an existing baseline error remains allowed, an introduced error blocks persistence while leaving the transaction available for correction and retry, a dependency change is evaluated against its dependant, and changed external inputs fail closed. Host integration coverage exercises conditional service and tool composition. Operational-mode acceptance coverage verifies that the validation tool is absent by default, appears only for enabled mutation modes and cannot be configured with inspection-only. Published-Host acceptance coverage verifies that a failed validation and commit do not write to disk, the same transaction can be corrected and committed, and a dependency break is detected in a dependant project. The dependency scenario measures `transaction-preview` and records both client-observed and server-reported cold and repeated validation durations against the same staged change. It applies a fixed 10-second ceiling to each validation measurement as an acceptance smoke test rather than a relative performance assertion. The manual scenario runner provides the repository-size evidence: its cost-isolated `compiler-validation` command starts a fresh validation-enabled Host for every iteration and compares preview with cold and repeated validation against one staged snapshot without imposing machine-specific thresholds. GuardClauses, Serilog and EF Core supply successful small-, medium- and large-repository baselines respectively. Serilog uses an explicit `net10.0` Workspace evaluation because its multi-target outer builds do not expose the compiler target; the selected framework is part of the report rather than an implicit benchmark assumption. Compile-safe internal EF Core renames provide both a foundational-project measurement with a wide dependant-project fan-out and a leaf-project measurement confined to one affected project, distinguishing solution size from change scope. The broader EF Core `DbContext` rename remains negative evidence for an expensive compiler rejection. The complete acceptance wrapper is required whenever those acceptance assets change.
