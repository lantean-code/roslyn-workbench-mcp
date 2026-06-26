# Unit Testing Rules (xUnit + Moq + AwesomeAssertions)

- Frameworks: xUnit, Moq, AwesomeAssertions.
- Use Moq for test doubles; do not introduce hand-written fake or stub implementations unless explicitly approved.
- Moq callbacks should only be used when verifying the same behaviour is impossible with `Verify`.
- Do not add comments to test code.
- Braces must never be omitted.
- Expression-bodied members are not permitted in tests.

## Naming
- Test class name: `<ClassName>Tests`
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
- Mocks used across tests are private readonly fields created with `Mock.Of<T>()`.
- Mocks that are local to a single test method should use `new Mock<T>()`.
- Shared test-only helpers belong in `Roslyn.Workbench.Mcp.TestSupport`.

## Test data conventions
- Strings use the property name as the value (not `nameof`), for example `request.Name = "Name"`.
- Dates use a fixed point in time: `2000-01-01 00:00` with the correct `DateTimeKind`. Adjust earlier or later than this when ranges or ordering are required.
- Numeric values must be contextually appropriate.

## Coverage and access
- Tests must cover 100% of the lines and branches of the implementation under test unless the user explicitly relaxes that rule.
- Never use reflection to invoke implementation code. Cover private or protected methods through normal execution flow only.
- If code cannot be reached via public methods or supported internal seams, consider asking to refactor.
- Do not add test-only hooks or methods to production code. If coverage gaps exist, ask for a refactor to expose the behaviour through normal flows.

## Clarification policy
- Do not make assumptions. If any referenced code or behaviour is unclear, ask for clarification before writing tests.

## Line endings
- Use CRLF line terminators for any files you write or modify.
- After editing any test file that is expected to use CRLF, run `unix2dos <changed files>` to normalize the entire file and eliminate any LF or mixed endings introduced by patching tools.
- Do not run `unix2dos` on files that are intentionally LF per `.gitattributes` or repository convention.
- Before finishing, verify every changed CRLF-governed file is `crlf` and not `mixed`.

## Formatting
- After modifying test files, run `dotnet format --include <changed files> --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp` for the files changed in the current task only.
- Do not format unrelated files.

## Test execution
- After each behaviour-affecting set of changes, follow the test execution instructions in the repository root `AGENTS.md`.
- If the change is docs-only or markdown-only and does not affect behaviour, test execution is optional unless explicitly requested.
- When contributing PR summaries or PR bodies, describe testing in terms of the coverage added or updated by the change, not just the commands executed.

## Anti-smell rules
- Do not inspect invocation internals in assertions. Avoid `Invocations.Count/Any/Where/Single/First/Last`, `Method.Name`, and `Arguments[...]` in test assertions.
- Prefer `Verify(...)` for Moq assertions.
- If invocation history must be reset between phases, use shared test infrastructure helpers rather than ad-hoc invocation-list manipulation.
- Do not use `Task.Delay(...)` for synchronization or timing in tests.
- Use deterministic waiting or polling primitives instead.

## Pre-flight checklist (must confirm all before generating tests)
- [ ] I am using xUnit, Moq, and AwesomeAssertions.
- [ ] No comments will be included in the test code.
- [ ] Class name is `<ClassName>Tests`.
- [ ] Namespace mirrors the product namespace with `.Test` inserted appropriately.
- [ ] Methods follow `GIVEN_..._WHEN_..._THEN_...` naming.
- [ ] For unit tests, `_target` is used only when one shared constructor setup serves most tests in the class; otherwise the system under test is created per test in a local `target`.
- [ ] Class-level mocks are `Mock.Of<T>()`; method-local mocks use `new Mock<T>()`.
- [ ] Strings use property names as values; dates use `2000-01-01 00:00` with correct `DateTimeKind`; numbers are sensible.
- [ ] No expression-bodied members; braces are always present.
- [ ] No reflection is used; private logic is covered through normal flows.
- [ ] Planned tests achieve 100% line and branch coverage unless the user has approved a lower bar.
- [ ] Any uncertainties have been raised and clarified.
- [ ] If coverage would require new public or test-only hooks, I have stopped and asked for a refactor approval.
- [ ] No invocation-internals inspection is used in assertions.
- [ ] No `Task.Delay(...)` is used for test synchronization.
