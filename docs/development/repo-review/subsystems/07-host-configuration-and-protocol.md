# Subsystem review: Host, configuration, protocol and composition

## Scope and relationships

This unit covers the executable entry point, startup composition, CLI/environment configuration, DI registrations, plugin package loading, MCP tool publication, schema generation/binding and server-owned workspace/transaction/status tools. It is the top production layer and consumes every lower subsystem.

## Implementation and boundary review

- `Program.Main` uses the Generic Host and stdio MCP transport. Startup resolves/validates configuration, builds the fixed Code Action catalogue, loads bundled/external plugins, registers all singleton services, hosted prerequisites and the top-level exception filter before running.
- CLI scalar options override environment values; invalid values generate explicit fallback warnings. Error-reporting consent ignores the environment by design and fails closed unless set through the command line/default.
- Input schemas and binding use the same web JSON naming/nullability metadata. Required arguments are checked case-insensitively before deserialisation, enum values are validated, and data-annotation validation produces structured argument errors.
- Four typed adapters keep plugin query/mutation and Code Action query/mutation acquisition/result semantics distinct. Server-owned lifecycle and transaction tools map Workspace outcomes without exposing implementation exceptions.
- Plugin discovery validates package containment, entry-point uniqueness/metadata and collisions before MCP registration. The Generic Host supplies the lifetime dependency used by caches and disposes singleton-owned resources.

## Consumers, DI and configuration

This is the composition root. All registrations are singleton because they coordinate process-wide workspaces, caches, catalogues and stores; request-specific state is carried in leases/contexts, not scoped services. Hosted startup initialises secure state, recovers unfinished commits, registers MSBuild and reports effective configuration before accepting MCP requests.

## Tests and findings

Host unit tests cover option precedence/fallback, binder/schema contracts, tool metadata/adapters and exception mapping and passed 341 fast-loop tests. Host integration has 59 passing tests, two failures caused by shared fixture RWMCP-004 and one independent stale discovery assertion RWMCP-005. No production Host DI or protocol mismatch survived validation.
