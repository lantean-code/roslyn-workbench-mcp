# Code Generation Rules

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
- `Roslyn.Workbench.Mcp` owns executable bootstrap, server startup, dependency composition, and server-owned lifecycle tools.
- `Roslyn.Workbench.Mcp.Contracts` owns shared request/response DTOs, schemas, selectors, result envelopes, and common contract types.
- `Roslyn.Workbench.Mcp.Workspace` owns workspace loading, transaction state, external-change handling, reload behaviour, and commit coordination.
- `Roslyn.Workbench.Mcp.Plugins` owns plugin abstractions, registration, tool metadata, and execution plumbing shared by plugins.
- `Roslyn.Workbench.Mcp.Plugins.Core` owns bundled first-party Roslyn query and mutation tool implementations.
- Query and mutation MCP tools must be implemented as plugins, even when shipped in the main server package.
- Plugins must not directly own or mutate host-only concerns such as the `MSBuildWorkspace`, transaction coordinator, file writer, or commit journal.
- Do not collapse host-owned lifecycle code into plugin projects, and do not move plugin tool logic into the executable host for convenience.

## Coding standards

### Naming
- Use PascalCase for classes, records, structs, interfaces, enums, methods, properties, events, and public fields.
- Use `_camelCase` for private fields and private constants.
- Use PascalCase for public constants.
- Use camelCase for local variables and parameters.
- Interfaces must begin with `I`.

### Formatting
- Braces on a new line and never omitted.
- Use file-scoped namespaces by default. Use block-scoped namespaces only when the file structure genuinely requires them.
- Use blank lines where appropriate to improve readability.
- Expression-bodied members are allowed only for get-only properties when they improve readability; methods must use block bodies.
- Member order:
  1. Constants
  2. Static fields and properties
  3. Private fields
  4. Private properties
  5. Public fields
  6. Public properties
  7. Constructors
  8. Public instance methods
  9. Private instance methods
  10. Public static methods
  11. Private static methods

### Coding practices
- Use `var` wherever possible unless it harms clarity.
- Enable and properly use nullable reference types.
- Always specify access modifiers, even when the default applies.
- Use `async` only when needed.
- Async methods should follow normal .NET naming and generally end with `Async`, except for well-known event handlers or framework-required signatures.
- Prefer LINQ for simple operations; use loops for complex logic or hot paths where clarity or allocation control matters.
- Do not use exceptions for flow control.
- Do not use fire-and-forget tasks (`async void`, discarded `Task`/`ValueTask`, or background work without explicit lifecycle management).
- Prefer immutable contract objects where it keeps request/response semantics clear.
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
- Static methods and classes are acceptable when they are truly stateless.
- Avoid partial classes in user code unless generated code or a framework requires them.
- Use `record` for data-only objects when value semantics are useful.
- Do not use positional record syntax.
- For records and classes with constructors, declare explicit properties and constructor bodies.
- Constructor parameter names must use `camelCase`.
- Extension methods are permitted and should follow standard naming conventions.

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
- After modifying source files, run `dotnet format --include <changed files> --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp` for the files changed in the current task only.
- Do not format unrelated files.

## Enforcement
- Generate C# code that follows these standards exactly.
- If existing code does not follow these rules, call it out explicitly before proceeding.

## Pre-flight checklist (agents must confirm)
- [ ] Standards here are applied to all generated code.
- [ ] Nullable reference types are enabled and used correctly.
- [ ] Public contract and extension-point APIs include XML docs with proper tags.
- [ ] Braces are never omitted; no expression-bodied methods.
- [ ] Async usage is justified and async method naming follows normal .NET conventions.
- [ ] Member order matches the specified list.
- [ ] Access modifiers are explicit everywhere.
- [ ] LINQ is used for simple operations; loops are used where they improve clarity or performance.
- [ ] No exceptions are used for flow control.
- [ ] Design follows DI, SOLID, DRY, and avoids security pitfalls.
- [ ] Positional records are not used; explicit properties and constructors are used and constructor parameters are camelCase.
- [ ] Query and mutation tools are kept in plugin projects, not server-owned lifecycle projects.
- [ ] Any conflicts with existing code or docs are reported for clarification.
