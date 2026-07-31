# Subsystem review: internal Code Action catalogue and replay workflow

## Scope and relationships

This unit covers `Roslyn.Workbench.Mcp.CodeActions`: provider composition/selection/policy, diagnostics, discovery, action references, deterministic replay, Fix All preparation, evaluation, staging and tool contracts. It depends on Workspace and Roslyn but is intentionally separate from the public Plugins extension model.

## Implementation and boundary review

- MEF composition locates the supported built-in providers and records availability. Policy filters excluded providers/actions during discovery; replay requires the same provider and uniquely matching title, equivalence key, action path, diagnostics and target span against the exact snapshot.
- References store recipes rather than live `CodeAction` objects. The bounded memory cache indexes workspace, transaction and snapshot identities and lifecycle observers invalidate references on close/reload/revision changes.
- Fix All preparation records scope and diagnostic identity, then replay recreates a provider `FixAllContext` with the current exact snapshot. Unsupported or ambiguous replay expires the reference.
- `CodeActionEvaluator` accepts exactly one `ApplyChangesOperation` plus the specifically recognised wrapping bookkeeping operation. Other side-effecting operations are rejected rather than executed.
- Candidate solutions flow through `CodeActionStager` and the Workspace mutation pipeline; CodeActions has no direct file-write boundary.

## Consumers, DI and configuration

Host creates a server-owned fixed catalogue and registers discovery, composition, reference, evaluator, replay and staging services as singletons. Reference lifetime/size and composition options are supplied through validated options.

## Tests and findings

Unit coverage spans controlled providers, discovery flattening, policy, reference eviction, replay ambiguity, Fix All scopes, evaluator operations and staging and passed 262 fast-loop tests. Both controlled and built-in integration workflows consume the shared component fixture and are currently blocked by RWMCP-004. No production Code Action finding survived validation.
