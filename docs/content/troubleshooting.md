# Troubleshooting and removal

## The client cannot start the server

Run `roslyn-workbench-mcp --version` from the same user account that starts the MCP client. If the command is not found, check the global .NET tool installation and whether the client's environment includes its executable directory. Restart the client after changing its environment, or configure the absolute executable path.

The server speaks MCP on standard input/output; it is not an interactive command prompt. Operational messages are on stderr. Check the client's server log for startup and prerequisite errors without posting private paths or credentials publicly.

## A Workspace will not open

Confirm that the path is accessible, the repository is trusted and the required project SDKs/build tooling are installed. Inspect `server-status` and the returned diagnostics. Unsupported languages or project types may be skipped; at least one supported SDK-style C# project must remain. A build outside the server can help distinguish project restoration/evaluation problems from MCP configuration.

State-directory failures require a writable, supported location. Do not bypass owner-only permissions or remove unresolved recovery records to force startup. See [Configuration](configuration.md) and [Workspaces and transactions](workspaces-and-transactions.md).

## A selector or Code Action became stale

Follow the tool's continuation: check Workspace status, reload when appropriate, and resolve the location/symbol or discover the action again. Do not reuse coordinates, snapshot identities or opaque references after unrelated source changes or a server restart. Finish or roll back an active transaction before attempting a reload that requires a non-transactional Workspace.

## An error-report prompt did not appear

The client may not support elicitation or may block it through its approval policy. `ApprovalUnavailable` and `ErrorReportNotApproved` mean nothing was sent. Enable manual MCP approvals in the client if desired, then prepare a fresh report. There is no tool override that silently bypasses client consent. See [Error reporting and privacy](error-reporting.md).

## Upgrade or remove

Before upgrading, finish or discard active transactions, stop the MCP server and review the target release's compatibility notes. Update the .NET tool through the same authenticated package source where required, then restart the client and rediscover the tool catalogue. Process-local Workspaces and references do not survive restart.

To remove the installed tool:

```bash
dotnet tool uninstall --global Roslyn.Workbench.Mcp
```

Remove its MCP client configuration as well. Uninstalling the tool does not remove your source repositories or the separately stored Host state. Keep any unresolved recovery data until the affected Workspace has been safely recovered. Once all recovery is resolved and no server instance is running, the configured state directory can be removed deliberately if no longer needed. Source-build users can remove their chosen publish directory after stopping the process.

Use the [support routes](https://github.com/lantean-code/roslyn-workbench-mcp/blob/develop/SUPPORT.md) if the problem remains. Provide the version, client, platform, reproduction and safely redacted diagnostics.
