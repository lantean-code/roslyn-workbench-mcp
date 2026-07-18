# Integration Testing Stage 2 Results

Date: 17 July 2026

## Outcome

Stage 2 is complete. The repository now has production-independent acceptance coverage that starts the published executable as a child process and communicates with it over real stdio using the official MCP C# client.

## Acceptance boundary

- `Roslyn.Workbench.Mcp.AcceptanceTest` is part of the solution and is classified assembly-wide as `Category=Integration`.
- The project references xUnit v3, its runner, AwesomeAssertions and `ModelContextProtocol` only. It has no production project reference, integration-support reference, mocks or internal access.
- The project links the minimal checked-in SDK workspace asset as content and copies it into a unique scenario workspace. State is kept in a separate unique scenario directory.
- `ROSLYN_WORKBENCH_MCP_ACCEPTANCE_HOST_PATH` is the single supported input for selecting a published Host executable. Missing and non-existent values fail immediately with actionable messages.
- The project README gives explicit Debug and Release publish/test commands for Unix and Windows; it does not infer a configuration or search arbitrary build output.

## Process fixture

The fixture uses `StdioClientTransport` and `McpClient.CreateAsync`, captures stderr separately from protocol stdout, and applies explicit 45-second initialisation, 30-second invocation and 2-second forced-cleanup bounds. Test cancellation is linked from `TestContext.Current.CancellationToken`.

Client disposal owns transport disposal. The official transport waits for the configured fallback period before terminating a process that remains active. Completion details prove that the child exited and retain its process ID, exit code and stderr tail. Failures report the command, exit code and captured stderr. Failed roots are retained only when `ROSLYN_WORKBENCH_MCP_ACCEPTANCE_RETAIN_ROOT` is set to `true` or `1`; otherwise all scenario files are removed asynchronously.

## Accepted limitation: MCP C# client shutdown ordering

Status: Accepted for release on 2026-07-18

The initial acceptance run took 11 seconds on both Linux and Windows because the fixture configured a 10-second `StdioClientTransportOptions.ShutdownTimeout`. Inspection of MCP C# SDK 1.4.1 showed that `StdioClientSessionTransport` waits for the server process to exit before process disposal closes the redirected stdin stream. The Host therefore cannot observe EOF during that wait, and the client terminates it when the fallback expires.

The Host is configured correctly. `WithStdioServerTransport` registers the SDK's single-session hosted service, which requests application shutdown when its MCP session completes. A separate acceptance test now starts the published executable, closes stdin directly and asserts a natural zero exit. That test passes on Linux and Windows.

The repository now uses MCP C# SDK 1.4.1, the latest stable release checked on 2026-07-18. Its `StdioClientSessionTransport` still waits for the child process before redirected stdin is disposed, so the ordering limitation remains. The 2.0 line is prerelease and is not adopted solely to revisit acceptance-fixture cleanup.

The fixture fallback remains set to two seconds, with an explanatory code comment, so each isolated acceptance workflow does not inherit a longer teardown penalty. This means disposal through the official client normally terminates the acceptance Host after the fallback instead of demonstrating a graceful EOF-driven exit. It remains long enough to bound SDK cleanup but is not treated as evidence of graceful Host shutdown; the direct-EOF test owns that evidence. Revisit and remove the workaround when a supported stable upstream SDK closes stdin before waiting for process exit.

Upstream evidence: [ModelContextProtocol package versions](https://www.nuget.org/packages/ModelContextProtocol/) and [`StdioClientSessionTransport` in v1.4.1](https://github.com/modelcontextprotocol/csharp-sdk/blob/2b7fd35fbe58dfb9f00eae8b3393e1a7361b5e01/src/ModelContextProtocol.Core/Client/StdioClientSessionTransport.cs).

## Protocol evidence

One real-process scenario proves:

- MCP initialisation completes against the published executable;
- `tools/list` includes representative server-owned (`server-status`), bundled Plugin (`search-symbols`) and Code Action (`list-code-actions`) tools;
- `server-status` returns structured JSON with server/Roslyn versions, a tool count matching the published catalogue and available Code Actions;
- Plugins.Core is reported as `roslyn.workbench.core`; and
- Code Actions is not reported as a plugin.

Separate setup scenarios prove that an absent executable path fails without starting a process and a deliberately broken `dotnet` startup terminates promptly with its command, exit code and stderr in the diagnostic.

## Verification

- acceptance project build: succeeded with no warnings;
- acceptance suite against the Debug published Host: 5 passed in approximately 3 seconds;
- Windows Debug publish and acceptance suite through Windows .NET 10.0.302: 5 passed in approximately 3 seconds;
- Windows verification left no Host child process or scenario content behind;
- `Category!=Integration&Category!=Audit` against the acceptance project: no matching tests;
- the same fast-loop filter against the Windows acceptance build: no matching tests;
- overriding the acceptance project to `TestCategory=Audit`: rejected by `ValidateTestProjectCategory`;
- complete solution restore and build: succeeded with no warnings;
- complete repository suite, including acceptance: 1,975 passed; and
- no acceptance scenario roots remained after the verification runs.
