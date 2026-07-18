# Integration Testing Stage 8 Results

Date: 18 July 2026

## Outcome

Stage 8 completes the integration-testing redesign. The dated architecture audit found no unresolved boundary, lifetime, isolation, scenario-retention or contract issue. Canonical strategy, topology and capability ownership now describe the implemented suite rather than its historical predecessor.

Before the audit, `Microsoft.Build` and `Microsoft.Build.Framework` were updated from 18.7.1 to 18.8.2. Restore, build and the MSBuild-sensitive integration suites passed, so no compatibility issue was found with the new API-package versions.

## Audit Summary

- 30 component-integration classes retain 117 named real-boundary tests.
- 8 acceptance classes retain 10 published-process tests through the official MCP client over stdio.
- 5 Unit/Contract projects retain 1,732 tests; the 4 component projects, acceptance project and audit project are physically and categorically separate.
- Acceptance has no production project references, mocks, internal access or direct tool invocation.
- Component support does not reconstruct Host or MCP transport composition.
- All mutable fixtures, state roots, loaded workspaces and child processes have deterministic ownership and cleanup.
- Transaction durability/recovery/locking, real plugin assembly/private dependency loading, and controlled/bundled Code Action evidence remain present.
- Contract tests retain MCP names, schemas and JSON shapes; published acceptance samples the same public catalogue and workflows.

The detailed class-to-boundary record is `TestArchitectureReaudit-2026-07-18.md`.

## Portability Correction

The audit found that the acceptance fixture-copy target still derived an external plugin package from `$(ArtifactsPath)`. That property is an agent-only output-isolation convention and is intentionally absent from CI and contributor commands. The target now obtains the fixture assembly through MSBuild `GetTargetPath` and derives its dependency manifest and private dependency beside the resolved assembly. A build from a fresh output root copied exactly those three assets.

## Final Counts and Performance

| Layer | Projects | Classes or tests | Tests |
| --- | ---: | ---: | ---: |
| Unit/Contract | 5 | — | 1,732 |
| Component integration | 4 | 30 classes | 117 |
| Published-Host acceptance | 1 | 8 classes | 10 |
| Compatibility audit | 1 | — | 93 |
| Total | 11 | — | 1,952 |

Stage 7 measurements remain the final performance evidence: 27.57 seconds median sequential component time, 12.84 seconds concurrent matrix critical path, component median peak memory from 344.2 MiB to 506.5 MiB, and a 53.28-second/1,074.2 MiB compatibility-audit median. The component suite is 42.8% faster than the Stage 0 baseline.

## Verification

Final verification results:

- restore: passed
- build: passed with zero warnings and zero errors
- Unit/Contract: 1,732 passed
- Workspace integration: 52 passed
- Plugins.Core integration: 20 passed
- CodeActions integration: 5 passed
- Host integration: 40 passed
- published-Host acceptance: 10 passed
- Code Action compatibility audit: 93 passed
- complete solution: 1,952 passed
- project/category, diff and CRLF checks: passed

No C# source or test file changed during Stage 8, so the changed-C# formatting step was not applicable. The two package versions, acceptance output resolution and documentation are the complete Stage 8 change set.
