# Unit Testing Rules (xUnit + Moq + AwesomeAssertions)

Cross-project test ownership and execution-path policy are defined in `../docs/development/TestingStrategy.md`; this file defines the implementation rules for tests under `test`.

- Frameworks: xUnit, Moq, AwesomeAssertions.
- Use Moq for test doubles; do not introduce hand-written fake or stub implementations unless explicitly approved.
- Moq callbacks should only be used when verifying the same behaviour is impossible with `Verify`.
- Write self-documenting test code. Do not add structural comments such as `Arrange`, `Act`, or `Assert`. Use inline comments sparingly when needed to explain a non-obvious constraint, deliberate exception, or justification that naming and code structure cannot convey.
- Braces must never be omitted.
- Expression-bodied members are not permitted in tests.

## Naming
- Test class name: `<ClassName>Tests`
- Integration test class name: `<ClassName>IntegrationTests`
- Test namespace should mirror the product namespace with `.Test` inserted into the project-specific root namespace.
  Example:
  - Product: `Roslyn.Workbench.Mcp.Workspace.Transactions`
  - Tests: `Roslyn.Workbench.Mcp.Workspace.Test.Transactions`
- Test method names use Given-When-Then: `GIVEN_StateOfItem_WHEN_PerformingOperation_THEN_ShouldBeExpectedState`

## Test class structure
- For non-component unit tests, use a readonly field named `_target` only when one shared constructor setup genuinely serves most tests in the class.
- When `_target` is used, construct it in the test class constructor.
- When constructor arguments, dependency behaviour, or lifetime vary per test and local construction is the common case, do not force a class-level `_target`; construct the system under test inside each test and store it in a method-local variable named `target`.
- Local `target` instances are allowed in classes that also use `_target` when they are the minority case and the altered construction is part of the scenario under test.
- Create mocks with `new Mock<T>()` so their setup, object access and verification remain explicit.
- Mocks used across tests are private readonly `Mock<T>` fields constructed in the test class constructor.
- Mocks local to a single test method are method-local `Mock<T>` variables.
- Shared test-only helpers belong in `Roslyn.Workbench.Mcp.TestSupport`.
- Unit tests must keep collaborator mocks visible in the test class. Configure `Mock<T>` instances in the class or test method rather than hiding dependency setup behind opaque harnesses.
- Unit tests may use small factory methods that return configured `Mock<T>` instances or wire visible mocks together, for example creating a `Mock<IQueryContext>` from class-level mocks.
- Avoid test harnesses, builders, or helper layers in unit tests unless their sole purpose is to provide repeatable Moq-based objects without obscuring the dependency setup under test.
- Unit tests should prefer Moq over hand-written fakes or stubs. If Moq cannot reach the behaviour and a fake seems necessary, stop and assess whether the production seam is wrong before introducing a fake.
- If a helper creates reusable non-mock test data or Roslyn-owned objects and is likely to be needed by more than one test class, move it into `Roslyn.Workbench.Mcp.TestSupport` instead of leaving it as a local helper inside one test class.
- Keep the split explicit: shared helpers may create Roslyn data objects or repeatable mock graphs, but scenario-specific `Setup(...)`, `Verify(...)`, assertions, and branch-specific configuration stay in the test class.

## Tool unit tests
- Tool unit tests are the default for query and mutation tools. They belong in the normal `*.Test` project owned by the implementation, not the integration test project.
- Keep the two tool systems explicit: third-party and bundled ordinary tools use Plugins/Plugins.Core contexts, while internal Code Action tools use CodeActions contexts and catalogues. Tests must not adapt one through the other.
- Host owns four transport adapters: plugin query, plugin mutation, Code Action query and Code Action mutation. Each adapter requires focused Host unit coverage for its type-specific acquisition, result mapping and staging behaviour.
- Unit test classes for tools use the `Tests` suffix. Integration coverage for tools uses the `IntegrationTests` suffix.
- Test the public entry point flow in actual runtime order, starting at `ExecuteAsync(...)` and then covering each branch reached through private helpers via normal execution.
- For branching tool handlers, prefer one test per reachable branch or outcome split so the logical flow is obvious from top to bottom.
- Tool test method names must describe the specific branch being exercised, for example `GIVEN_ResolveDocumentsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult`.
- When a shared base handler already owns a branch such as top-level cancellation handling, do not duplicate that branch expectation in every concrete tool test class. Test the shared handler once in its own unit tests.
- Unit tests for tools must keep `Mock<T>.Setup(...)` and `Verify(...)` calls visible in the test class. Shared helpers may wire common mock graphs together, but they must not hide scenario-specific setups, callbacks, or verifies.
- Query and mutation context helpers are acceptable when they only construct visible mocks and connect them together, for example `QueryContextMockHelper` or `MutationContextMockHelper`.

## Real Roslyn objects in unit tests
- Moq remains the default. Use real in-memory Roslyn objects only when the behaviour under test depends on Roslyn syntax, semantic model, compilation, symbol search, or solution graph behaviour that Moq cannot represent faithfully.
- Real Roslyn helpers must be dedicated to creating Roslyn objects only. They must not become general tool harnesses or hide test scenario setup.
- Prefer factory-based creation for real Roslyn test objects. A single factory may create multiple narrow result shapes, for example a document-scoped object and a solution-scoped object.
- Reusable Roslyn creation helpers such as diagnostic factories, location factories, symbol-reference factories, or document/solution factories belong in shared test support rather than individual tool test classes.
- Keep Roslyn helper shapes narrow and purpose-specific:
  - document-scoped helpers for single-document syntax or semantic scenarios
  - solution-scoped helpers for multi-document, multi-project, or project-reference scenarios
- Do not expand unrelated helpers such as generic workspace or context builders to absorb Roslyn-specific behaviours if that blurs their purpose.
- If a unit test uses real in-memory Roslyn objects, the surrounding collaborators must still be mocked explicitly unless the test is intentionally being classified as integration coverage.
- In unit tests, real Roslyn objects are allowed only as Roslyn data inputs or Roslyn-owned state, for example `Solution`, `Project`, `Document`, `Compilation`, `SemanticModel`, `SyntaxNode`, `Location`, and `ISymbol`.
- In unit tests, production collaborators other than the system under test must not be replaced with real runtime implementations just because those implementations are convenient. Keep host and runtime services mocked, including helpers such as `IWorkspaceResolver`, coordinators, workflows, project-structure services, and execution services.
- Test helpers must not instantiate real production service implementations for unit tests. If a helper creates a real runtime collaborator instead of a Roslyn data object, the test is no longer following the unit-test rules and must be reworked or reclassified.

## Test data conventions
- Strings use the property name as the value (not `nameof`), for example `request.Name = "Name"`.
- Dates use a fixed point in time: `2000-01-01 00:00` with the correct `DateTimeKind`. Adjust earlier or later than this when ranges or ordering are required.
- Numeric values must be contextually appropriate.

## Coverage and access
- Tests must cover 100% of the lines and branches of the implementation under test unless the user explicitly relaxes that rule.
- Approved exception: defensive null-guard branches that protect Roslyn-owned APIs may remain below 100% when all of the following are true:
  - the branch cannot be reached through the real public execution flow of the tool
  - covering it would require artificial fake or stub Roslyn runtime objects, reflection, or test-only seams
  - removing the guard would increase production risk by replacing defensive handling with a possible `NullReferenceException`
  - the specific gap is documented in the active audit or inventory document
- Never use reflection to invoke implementation code. Cover private or protected methods through normal execution flow only.
- If code cannot be reached via public methods or supported internal seams, consider asking to refactor.
- Do not add test-only hooks or methods to production code. If coverage gaps exist, ask for a refactor to expose the behaviour through normal flows.
- Do not reshape production contracts or runtime design to make assertions easier. Tests must follow the production API and behaviour, not the other way around.

## Clarification policy
- Do not make assumptions. If any referenced code or behaviour is unclear, ask for clarification before writing tests.

## Line endings
- Use CRLF line terminators for any files you write or modify.
- After editing any test file that is expected to use CRLF, run `unix2dos <changed files>` to normalize the entire file and eliminate any LF or mixed endings introduced by patching tools.
- Do not run `unix2dos` on files that are intentionally LF per `.gitattributes` or repository convention.
- Before finishing, verify every changed CRLF-governed file is `crlf` and not `mixed`.

## Formatting
- After modifying test files, run `dotnet format --include <changed files>` for the files changed in the current task only, following the environment-specific artifacts-path rule in the repository root `AGENTS.md`.
- Run the full .NET analyzer build defined in the repository root `AGENTS.md` and address applicable `CAxxxx` diagnostics in every changed test file. A normal build with zero warnings is not sufficient because the IDE reports additional default-disabled rules.
- Retain test-specific clarity and the repository's test conventions when evaluating analyzer suggestions; explicitly justify any diagnostic intentionally left in a changed test file.
- Do not format unrelated files.

## Test execution
- After each behaviour-affecting set of changes, follow the test execution instructions in the repository root `AGENTS.md`.
- If the change is docs-only or markdown-only and does not affect behaviour, test execution is optional unless explicitly requested.
- When contributing PR summaries or PR bodies, describe testing in terms of the coverage added or updated by the change, not just the commands executed.

## Anti-smell rules
- Do not add tests that pass `null!` to constructor dependencies of internal DI-created types or to non-nullable parameters on internal methods. Production code must rely on nullable warnings and controlled composition for those contracts rather than adding redundant null-guard branches.
- Do not inspect invocation internals in assertions. Avoid `Invocations.Count/Any/Where/Single/First/Last`, `Method.Name`, and `Arguments[...]` in test assertions.
- Prefer `Verify(...)` for Moq assertions.
- If invocation history must be reset between phases, use shared test infrastructure helpers rather than ad-hoc invocation-list manipulation.
- Do not use `Task.Delay(...)` for synchronization or timing in tests.
- Use deterministic waiting or polling primitives instead.

## Test taxonomy
- `Unit` tests are the default. They must not create temporary projects, open real workspaces, or drive real coordinator or transaction flows.
- `Contract` tests deliberately lock schema shape, validation rules, serialisation, MCP metadata, or the supported public plugin surface. Contract tests live with the production assembly that owns the contract; there is no shared Contracts test project.
- `Integration` tests use the real file system, Roslyn workspace, coordinator, plugin assembly discovery, transaction pipeline, Host composition, MCP publication, or an equivalent multi-component runtime flow.
- `Audit` tests validate built-in provider coverage, replay families and promotion ledgers. Source-governance checks belong in the normal fast architecture suite.
- New tests that create temporary directories, open real workspaces, or execute full tool flows must be marked with `[Trait("Category", "Integration")]` unless they are specifically Roslyn compatibility-audit coverage, in which case use `[Trait("Category", "Audit")]`.
- Schema locks and deliberate public-surface metadata locks should be marked with `[Trait("Category", "Contract")]` when they are not ordinary behaviour-focused unit tests. Do not use reflection to lock internal runtime shapes or to compensate for missing behavioural coverage.
- Roslyn fixture or coordinator coverage belongs in the integration test projects and should use `IntegrationTests` class suffixes, not `Tests`.

## Architecture boundary tests
- Prefer compilation, assignability, project-reference inspection and observable behaviour over reflection-only shape assertions.
- Workspace tests own neutral execution-context and separate-stager behaviour.
- Plugins tests own typed visitor dispatch, plugin-service adaptation and Workspace proposal/result mapping.
- CodeActions tests own its internal catalogue, typed visitor dispatch, Code Action-only contexts and Workspace proposal/result mapping.
- Host tests own MCP binding, schemas, publication and the four closed generic transport adapters.
- Integration tests own plugin discovery, reserved-name collisions, full Host composition and the absence of CodeActions from plugin status.

## Execution policy
- Default local development loop: run unit and contract coverage, excluding integration and audit categories.
- Integration coverage should run for touched areas during development and in CI for broader regression confidence.
- Audit coverage should run in broader CI or release gates, not in the default local loop.
- Preferred fast-loop command: `dotnet test --filter "Category!=Integration&Category!=Audit"`, with the WSL-specific artifacts path from the repository root `AGENTS.md` when required.

## Pre-flight checklist (must confirm all before generating tests)
- [ ] I am using xUnit, Moq, and AwesomeAssertions.
- [ ] Any test-code comment explains necessary non-obvious intent; no structural or narration comments are included.
- [ ] Class name is `<ClassName>Tests`.
- [ ] Namespace mirrors the product namespace with `.Test` inserted appropriately.
- [ ] Methods follow `GIVEN_..._WHEN_..._THEN_...` naming.
- [ ] For unit tests, `_target` is used only when one shared constructor setup serves most tests in the class; otherwise the system under test is created per test in a local `target`.
- [ ] All mocks use explicit `new Mock<T>()` construction; shared mocks are readonly fields and local mocks are method-local variables.
- [ ] Strings use property names as values; dates use `2000-01-01 00:00` with correct `DateTimeKind`; numbers are sensible.
- [ ] No expression-bodied members; braces are always present.
- [ ] No reflection invokes implementation code or locks an internal runtime shape; any deliberate public-surface metadata lock is a Contract test.
- [ ] Planned tests achieve 100% line and branch coverage unless the user has approved a lower bar.
- [ ] Any uncertainties have been raised and clarified.
- [ ] If coverage would require new public or test-only hooks, I have stopped and asked for a refactor approval.
- [ ] No invocation-internals inspection is used in assertions.
- [ ] No `Task.Delay(...)` is used for test synchronization.
