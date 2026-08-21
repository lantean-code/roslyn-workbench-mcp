# Code Generation Rules

## Review Agent isolation

- Any Review Agent review of production source must be performed by a fresh context-free subagent in accordance with the repository-root `AGENTS.md`; the primary implementation agent must not perform that review itself.
- The review subagent is read-only and must inspect the affected source with its direct dependencies, consumers, contracts, configuration and tests before reporting findings.

## Expectations

- Code must be technically precise, unambiguous, and avoid bad practices.
- Keep code consistent with the coding standards below.
- Follow Microsoft's official best practices for C#, .NET, Roslyn, and the MCP SDK.
- Adhere to SOLID and DRY principles.
- Avoid security vulnerabilities and common pitfalls.
- Write clean, self-documenting, readable code; use inline comments only where needed.
- Always include XML documentation on public APIs that define contracts, extension points, or externally consumed behaviour.
- Structure error and exception messages clearly, with correct grammar and punctuation.
- Design thoughtfully with proper async usage, memory safety, immutability where appropriate, and dependency injection.
- Prioritize maintainability, testability, and scalability.

## Project boundaries

- `Roslyn.Workbench.Mcp` owns executable bootstrap, server startup, dependency composition, MCP schemas and envelopes, transport adapters, and server-owned lifecycle tools.
- `Roslyn.Workbench.Mcp.Abstractions` owns the minimal public Workspace selectors, result models, resolver contracts, and project/query service contracts required by third-party plugin signatures. It must not depend on implementation projects or packages beyond the minimal Roslyn Workspaces API required by those signatures.
- `Roslyn.Workbench.Mcp.Workspace` owns selector resolution implementations, neutral execution leases, transaction state, external-change handling, reload behaviour, and commit coordination.
- `Roslyn.Workbench.Mcp.CodeActions` owns internal Code Action contracts, catalogue metadata, execution contexts, and workflows. It may depend on Workspace but not Plugins.
- `Roslyn.Workbench.Mcp.Plugins` owns the public third-party plugin API, plugin metadata, typed registrations, execution services, and Workspace context adaptation. Its public signatures may depend on Abstractions but must not expose Workspace implementation types; its internal adapters may depend on Workspace, but not CodeActions or the MCP SDK.
- Keep the `Roslyn.Workbench.Mcp.Plugins` root namespace focused on the core third-party author experience: plugin entry points, handler and context contracts, execution results, and registration/configuration entry points. Place supplementary public APIs in responsibility-based subnamespaces. Place internal or server-used implementation types in responsibility-based subnamespaces unless a documented author-facing reason requires the root.
- `Roslyn.Workbench.Mcp.Plugins.Core` owns bundled inspection contracts and first-party plugin implementations.
- Internal Code Action tools must not be registered or executed through the plugin system.
- Plugins must not directly own or mutate host-only concerns such as the `MSBuildWorkspace`, transaction coordinator, file writer, or commit journal.
- Do not collapse host-owned lifecycle code into plugin projects, and do not move plugin tool logic into the executable host for convenience.
- Reflection-based plugin discovery and generic materialisation are permitted only during Host startup. Plugin request execution must use prebuilt strongly typed adapters; do not use `MethodInfo.Invoke`, `Delegate.DynamicInvoke`, runtime generic construction, `Activator`, or runtime handler-interface inspection in a tool execution path.

## Coding standards

### Naming

- Use PascalCase for classes, records, structs, interfaces, enums, methods, properties, events, and public fields.
- Use `_camelCase` for private fields and private constants.
- Use PascalCase for public constants.
- Use camelCase for local variables and parameters.
- Interfaces must begin with `I`.
- Use the `Default` prefix only for an implementation that is deliberately provided as the default among replaceable alternatives. Name a sole internal implementation directly after its responsibility.

### Formatting

- Braces on a new line and never omitted.
- Use file-scoped namespaces by default. Use block-scoped namespaces only when the file structure genuinely requires them.
- Keep generic type declarations, generic method declarations, generic method invocations and generic type argument lists on one physical line regardless of length. Never place generic type arguments on separate lines.
- Keep tuple type declarations on one physical line regardless of length. Never place tuple elements on separate lines.
- Use blank lines where appropriate to improve readability.
- Insert a blank line after any statement that spans multiple lines before beginning the next statement.
- Prefer expression-bodied syntax for simple get-only properties. Methods and all other members must use block bodies.
- Member order:
  1. Constants
  2. Static fields and properties
  3. Private fields
  4. Public fields and properties
  5. Internal fields and properties
  6. Protected fields and properties
  7. Private properties
  8. Constructors
  9. Public instance methods
  10. Internal instance methods
  11. Protected instance methods
  12. Private instance methods
  13. Public static methods
  14. Internal static methods
  15. Protected static methods
  16. Private static methods
- Keep each member-order group contiguous; do not interleave members from different accessibility groups.

### Coding practices

- Use `var` wherever possible unless it harms clarity.
- Deconstruct tuples when their elements are consumed separately and deconstruction makes the subsequent code clearer.
- Enable and properly use nullable reference types.
- Do not add runtime null guards for constructor dependencies of internal DI-created types or for non-nullable parameters on internal methods. Nullable annotations, warnings as errors and DI composition are the contract; guards such as `throw new ArgumentNullException(...)` and `ArgumentNullException.ThrowIfNull(...)` add unreachable branches and low-value tests. Retain runtime argument validation at public or externally callable boundaries where callers are not controlled by the application.
- Nullable enforcement does not replace meaningful validation of non-null values. Retain semantic guards when values can still be invalid at runtime, for example blank paths or identifiers, invalid ranges, malformed values, and unsupported enum alternatives. `ArgumentException.ThrowIfNullOrWhiteSpace(...)` is appropriate when the contract requires meaningful text rather than merely a non-null reference; do not add it solely to duplicate nullable enforcement.
- Do not use the null-forgiving operator (`!`) in production code as a routine way to silence nullable analysis. Express the invariant in the type system instead: validate inputs, use required members or constructors, retain a checked local, pattern-match nullable values, or annotate state relationships with attributes such as `MemberNotNull`, `MemberNotNullWhen`, and `NotNullWhen`.
- A production null-forgiving operator is permitted only when the invariant cannot be expressed or observed by the compiler, no safer API shape is practical, and the reason is exceptional and explicitly documented next to the use. Reflection and framework boundary code must still validate the runtime result before relying on it.
- Always specify access modifiers, even when the default applies.
- On an internal type, declare members that form the type's normal usable surface as public; do not repeat `internal` merely because the containing type is internal. Use internal member accessibility only when it expresses a deliberately narrower boundary, such as a reflection-hidden helper, controlled construction seam, or cross-type coordination method.
- Use `async` only when needed.
- Async methods should follow normal .NET naming and generally end with `Async`, except for well-known event handlers or framework-required signatures.
- Place `CancellationToken` last in every production method signature, including signatures where trailing optional parameters would be exempt from `CA1068`. Prefer required nullable parameters at internal boundaries when optional syntax would force the token out of the final position; do not add forwarding overloads solely to preserve optional arguments.
- Do not use `ConfigureAwait(false)`. Production code in this repository executes within the console-hosted application, which does not install a synchronization context; await tasks directly.
- Assign an awaited result to a clearly named local before querying it or accessing members. Avoid constructs such as `(await operation).Any(...)`; separate the asynchronous operation from the subsequent synchronous processing.
- Do not perform asynchronous or otherwise complex work directly inside a conditional expression. Assign the result to a clearly named local before evaluating the condition. Simple predicate calls and conventional `Try*` calls may remain in conditions when the complete expression is immediately understandable.
- Prefer LINQ for simple operations; use loops for complex logic or hot paths where clarity or allocation control matters.
- Do not use LINQ to compress multi-stage logic at the expense of readability. Use named intermediate stages and explicit loops when filtering, ordering, limiting, projection, or allocation control combine; keep LINQ for short operations that remain easier to verify than the equivalent loop.
- Keep conditional and null-coalescing expressions simple. Do not perform non-trivial work or invoke methods on both branches or operands; use named intermediate values or ordinary branching when the alternatives require separate operations or nested construction.
- Keep methods focused on one clearly described operation. When a method contains several logical stages, substantial branching, or a long sequence that is difficult to verify as a whole, extract descriptively named helpers for the distinct stages.
- Name private helpers for their specific responsibility. Do not reuse a broader public method name when the private helper performs a narrower operation and the shared name would make call sites or stack traces ambiguous.
- Do not construct meaningful objects inline inside another constructor, factory, wrapper, or complex return expression. Assign each constructed context, lease, scope, store, options object, identity component, or other meaningful value to its own clearly named local before assembling and returning the completed result.
- Do not use exceptions for flow control. Expected validation failures, unavailable capabilities, contention, malformed external input, and other anticipated outcomes must be represented by explicit results, diagnostics, status values, `Try*` patterns, or ordinary branching.
- Do not throw an exception locally only for a caller or enclosing `catch` to translate it into an expected result. Accumulating validation should return all applicable diagnostics instead of throwing on the first finding.
- Exceptions remain appropriate for violated internal invariants, impossible states, cancellation, unsupported platforms, unexpected failures, and framework or operating-system APIs whose only failure channel is an exception. Boundary code may catch and translate those genuine exceptions, but the exception must not be the designed success/failure discriminator for a routine workflow.
- Do not use fire-and-forget tasks (`async void`, discarded `Task`/`ValueTask`, or background work without explicit lifecycle management).
- When application logging is necessary, use source-generated `LoggerMessage` methods instead of `LoggerExtensions.Log*` calls. Assign a stable event ID and log level, use named structured placeholders rather than interpolated strings, and pass exceptions through the generated method's `Exception` parameter when applicable. The partial type and method declarations required by the logging source generator are an approved use of partial code.
- Prefer immutable contract objects where it keeps request/response semantics clear.
- Types that represent mutually exclusive outcomes, or whose status properties imply member nullability, must expose get-only state, receive the complete state through a constructor, and provide named static factories for every valid outcome. A non-generic invariant-bearing type must keep that constructor private. A generic invariant-bearing type must place its factories on a non-generic companion type declared immediately after the generic type in the same file and use the narrow internal constructor required by that companion. Public contract boundaries must validate runtime invariants; internal-only types must follow the repository policy against redundant argument-null guards. Do not suppress CA1000 for outcome factories. Treat `private init` on these invariant-bearing types as a design smell: restricted object initializers do not make the valid state combinations explicit at the construction boundary.
- Avoid hidden ambient state. Pass required collaborators explicitly.

### Roslyn and MCP design rules

- Preserve snapshot-precondition semantics. Do not reinterpret stale spans, locations, or symbols against a newer workspace snapshot.
- Query tools must be read-only and must not write files or mutate host state.
- Mutation tools must stage candidate changes through the transaction pipeline; they must not write directly to disk.
- Return structured contract results, not ad-hoc prose payloads.
- Keep tool names, metadata, request shapes, and result envelopes aligned with the design docs.
- Prefer Roslyn semantic APIs and symbol-aware transforms over text-based code manipulation whenever the operation is inherently semantic.
- Prefer preview or analysis phases before applying refactors when Roslyn-backed tooling supports it.

### Design

- Use constructor injection only unless a framework API requires otherwise.
- Production services must depend on narrow interfaces for injected application and runtime collaborators, including internal collaborators that currently have only one implementation. Register these dependencies as interface-to-implementation mappings. Concrete constructor dependencies are permitted only for passive configuration or state objects with no operational service responsibility, immutable data objects, or types whose construction is controlled by a framework API.
- Dependency-injection registration methods must not use factory delegate overloads. Register interface-to-implementation mappings, implementation types, or instances directly so construction remains container-visible and validation can inspect the dependency graph. Registering an implementation type directly does not permit another production service to depend on that concrete operational service. When one stateful concern must support multiple service contracts, split the contract responsibilities into focused services and share a separately registered state collaborator instead of resolving and aliasing a concrete service through an `IServiceProvider` delegate.
- Static methods and classes are acceptable when they are truly stateless.
- Avoid partial classes in user code unless generated code or a framework requires them.
- Use `record` for data-only objects when value semantics are useful.
- Do not use positional syntax for records or record structs.
- Do not use primary constructors as a shortcut for state-bearing classes or structs.
- For records, record structs, classes and structs with constructor-supplied state, declare explicit properties or fields and explicit constructor bodies.
- Constructor parameter names must use `camelCase`.
- Extension methods are permitted and should follow standard naming conventions.
- Do not change production contracts, inheritance, implemented interfaces, or other runtime design purely to simplify tests. Production design must drive the tests, and tests must adapt to the production API.

### Documentation

- XML documentation comments are required on public APIs that form contracts, plugin extension points, or other externally consumed behaviour.
- Include `<summary>`, `<param>` where applicable, and `<returns>` when needed.
- Use inline comments sparingly and only to explain complex or non-obvious logic.
- Place attributes one per line.
- Only one type per file; the file name must match the type.
  - Exception: multiple small, strongly related generic variants of the same type may share a file when it materially improves readability.

## Line endings

- Use CRLF line terminators for any files you write or modify.
- After editing any source file that is expected to use CRLF, run `unix2dos <changed files>` to normalize the entire file and eliminate any LF or mixed endings introduced by patching tools.
- Do not run `unix2dos` on files that are intentionally LF per `.gitattributes` or repository convention.
- Before finishing, verify every changed CRLF-governed file is `crlf` and not `mixed`.
- After modifying source files, run `dotnet format --include <changed files>` for the files changed in the current task only, following the environment-specific artifacts-path rule in the repository root `AGENTS.md`.
- Run the SDK `latest-all` .NET analyzer build defined in the repository root `AGENTS.md` and address applicable `CAxxxx` diagnostics in every changed source file. A normal build with zero warnings is not sufficient because the IDE reports additional default-disabled rules.
- Treat analyzer fixes as design changes: preserve public contracts, architectural boundaries, and intentional abstractions rather than applying suggestions mechanically. If a diagnostic should remain, state the specific reason in the task hand-off.
- Do not format unrelated files.

## Enforcement

- Generate C# code that follows these standards exactly.
- If existing code does not follow these rules, call it out explicitly before proceeding.

## Pre-flight checklist (agents must confirm)

- [ ] Standards here are applied to all generated code.
- [ ] Nullable reference types are enabled and used correctly.
- [ ] Public contract and extension-point APIs include XML docs with proper tags.
- [ ] Production code contains no unjustified null-forgiving operators; nullable invariants are encoded and checked rather than suppressed.
- [ ] Braces are never omitted; no expression-bodied methods.
- [ ] Async usage is justified and async method naming follows normal .NET conventions.
- [ ] Member order matches the specified list.
- [ ] Access modifiers are explicit everywhere.
- [ ] LINQ is used for simple operations; loops are used where they improve clarity or performance.
- [ ] No exceptions are used for flow control.
- [ ] Design follows DI, SOLID, DRY, and avoids security pitfalls.
- [ ] Every injected application or runtime service is interface-typed unless it is an explicitly permitted passive object or framework-controlled type.
- [ ] Positional records and record structs are not used; state-bearing classes and structs do not use primary constructors; explicit properties, fields and constructor bodies are used and constructor parameters are camelCase.
- [ ] Query and mutation tools are kept in plugin projects, not server-owned lifecycle projects.
- [ ] The SDK `latest-all` .NET analyzer set has been run and applicable diagnostics in changed source files are fixed or explicitly justified.
- [ ] Any conflicts with existing code or docs are reported for clarification.
