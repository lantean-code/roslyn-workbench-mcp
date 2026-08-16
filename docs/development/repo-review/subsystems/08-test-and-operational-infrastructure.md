# Unit 8 — Test and operational infrastructure

Date: 2026-08-16

**Report status:** Completed.

## Scope and evidence

This review covered every test/support/fixture/asset project, the real component fixture, published Release Host acceptance infrastructure and wrappers, complete ScenarioRunner, CI workflows, suite categorisation, test-count enforcement and build/test configuration. Existing test implementations are coverage claims rather than fresh results; no command ran.

## Test architecture and real boundaries

Project naming and `test/Directory.Build.targets` assign Integration, Acceptance and Audit traits. CI runs solution-wide Unit/Contract tests, four component integration projects, published acceptance on Ubuntu and Windows, Workspace integration again on Windows, and a separate Code Action compatibility audit. Minimum TRX counts detect gross selection loss but are not exact behavioural inventory locks.

IntegrationTestSupport constructs real DI and Workspace services, materialises assets and crosses filesystem, MSBuild, Workspace lease, Roslyn execution and staging boundaries, but not Host binding, serialization or stdio. Acceptance publishes a Release Host, uses the official MCP client over stdio with isolated Workspace/state/plugin directories, exercises compiled plugin packages and retains process/stderr evidence on configured failures.

ScenarioRunner launches a separate Host against pinned external repositories, serialises cache access, isolates NuGet packages, runs protocol workloads, captures process/EventPipe evidence, restores repositories and writes results. It is a manual release tool without an owning test project and is absent from CI.

## Operational traces

First preparation clones and checks out the exact commit; repeated preparation validates HEAD and tracked cleanliness. External child processes drain both streams, kill the complete tree on cancellation/failure and await termination. Destructive scenarios restore tracked changes and newly created untracked files. Crash recovery crosses real staging, applied source observation, forced process termination, recovery manifest/artifacts, new Host startup and cleanup. EventPipe sessions have explicit stop/disposal paths.

The main weaknesses are in reusable-cache handling and failure evidence. Baseline untracked files are tracked only by pathname, so mutation/deletion can survive restoration. Scenario families write structured results only after successful iterations, so early failures can leave only an exception message and delete transient evidence. Option parsing accepts unknown names and silently applies defaults.

## Cross-check of production candidates

Current infrastructure does not disprove `RWMCP3-001`, `RWMCP3-003`, `RWMCP3-005`, `RWMCP3-006` or `RWMCP3-007`: no external wildcard-addition test, simultaneous multi-Workspace start, explicit-item add/delete commit/reload, malformed nonblank recovery admission or deterministic directory-swap test exists. Unit 4 gaps for nested CFG locations, extreme code-context lines, cross-document format ranges and rename-file commit/reload remain uncovered. Unit 5 lacks sibling-root collision and mixed throwing/successful provider tests.

## Candidates

### RWMCP3-016 — Baseline untracked files can be mutated without restoration

**Severity:** P2  
**Confidence:** High  
**Location:** `tools/Roslyn.Workbench.Mcp.ScenarioRunner/Repositories/RepositoryRestorer.cs:33-61`; repository cache validation that uses `git status --untracked-files=no`

Preparation may leave an untracked source loaded by MSBuild. The restorer records only baseline path membership; if a scenario changes or deletes that file, capture sees no newly added pathname, restoration does nothing, verification passes and later exact-commit runs reuse contaminated state. Record baseline identities/content, reject mutable untracked inputs or use disposable per-run worktrees.

### RWMCP3-017 — Scenario failures can discard the evidence needed to diagnose them

**Severity:** P2  
**Confidence:** High  
**Location:** `tools/Roslyn.Workbench.Mcp.ScenarioRunner/Application/ScenarioApplication.cs:267-278`

Family result writers run only after all warm-ups and measured iterations. An earlier workload, Host, EventPipe, restoration or validation failure reaches the outer catch, which emits only `exception.Message`; partial measurements and structured validation can be lost and the execution directory deleted. Persist a structured failure report with invocation, completed measurements, exception details, Host stderr, cleanup/validation outcomes and artifact paths before removing transient state.

### RWMCP3-018 — Unknown ScenarioRunner options silently run a different workload

**Severity:** P2  
**Confidence:** High  
**Location:** `tools/Roslyn.Workbench.Mcp.ScenarioRunner/Application/ScenarioOptions.cs:56-97`

Every unrecognised `--name value` pair enters the dictionary and is ignored. A misspelling such as `--iteratons 20` succeeds with the default five iterations; misspelled output, cache, duration or cancellation options similarly produce evidence for unintended settings. Validate option names and command applicability, reject ambiguous duplicates and cover misspellings/unsupported command-option combinations.

## Notable gaps

ScenarioRunner has no tests despite destructive restoration, process, EventPipe, crash and cache responsibilities, and CI executes no synthetic runner cycle. Crash recovery remains manual-only. Native directory-swap containment is unproved on both platforms. Test-count checks do not identify skipped tests, duplicated TRX or family-level loss. CI does not enforce the repository-required `latest-all` analyser build.

