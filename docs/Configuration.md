# Configuration

Roslyn Workbench accepts command-line options and equivalent environment variables. A command-line scalar takes precedence over its environment variable; when a scalar option appears more than once, the last value wins. Invalid values fall back to the documented default and are reported as `StartupConfigurationFallback` warnings.

| Command-line option | Environment variable | Default | Meaning |
| --- | --- | --: | --- |
| `--plugin-directory` | `ROSLYN_WORKBENCH_MCP_PLUGIN_DIRECTORY` | None | Adds plugin search roots. The option is repeatable; the environment value uses the platform path separator. Command-line and environment roots are combined and deduplicated. |
| `--default-max-results` | `ROSLYN_WORKBENCH_MCP_DEFAULT_MAX_RESULTS` | `100` | Positive Host compatibility baseline for third-party tools that have not established a curated request default. |
| `--code-action-reference-lifetime` | `ROSLYN_WORKBENCH_MCP_CODE_ACTION_REFERENCE_LIFETIME` | `00:05:00` | Positive invariant-culture `TimeSpan` controlling discovered Code Action reference lifetime, up to `1.00:00:00` (24 hours). |
| `--workspace-query-cache-size-limit` | `ROSLYN_WORKBENCH_MCP_WORKSPACE_QUERY_CACHE_SIZE_LIMIT` | `10000` | Workspace query-result capacity in Host-calculated retained-result units; supported range `5000`–`100000`. |
| `--plugin-query-cache-entry-limit` | `ROSLYN_WORKBENCH_MCP_PLUGIN_QUERY_CACHE_ENTRY_LIMIT` | `10000` | Plugin query-result capacity in entries; supported range `7500`–`50000`. |
| `--code-action-reference-cache-size-limit` | `ROSLYN_WORKBENCH_MCP_CODE_ACTION_REFERENCE_CACHE_SIZE_LIMIT` | `75000` | Replayable Code Action recipe capacity in retained-recipe units; supported range `40000`–`250000`. |
| `--workspace-query-cache-sliding-expiration` | `ROSLYN_WORKBENCH_MCP_WORKSPACE_QUERY_CACHE_SLIDING_EXPIRATION` | `01:00:00` | Positive invariant-culture `TimeSpan` for idle Workspace query-result expiry, up to 24 hours. |
| `--plugin-query-cache-sliding-expiration` | `ROSLYN_WORKBENCH_MCP_PLUGIN_QUERY_CACHE_SLIDING_EXPIRATION` | `01:00:00` | Positive invariant-culture `TimeSpan` for idle plugin query-result expiry, up to 24 hours. |
| `--max-transaction-revisions` | `ROSLYN_WORKBENCH_MCP_MAX_TRANSACTION_REVISIONS` | `20` | Positive maximum number of retained staged transaction revisions. |
| `--max-concurrent-queries` | `ROSLYN_WORKBENCH_MCP_MAX_CONCURRENT_QUERIES` | `2` | Positive maximum number of concurrent query leases. |
| `--tool-output-schema-mode` | `ROSLYN_WORKBENCH_MCP_TOOL_OUTPUT_SCHEMA_MODE` | `Omit` | `Omit` keeps `tools/list` compact; `Full` publishes generated family-specific output schemas. |
| `--state-directory` | `ROSLYN_WORKBENCH_MCP_STATE_DIRECTORY` | Per-user application state directory | Absolute or relative writable location for Host state and durable commit-recovery records. The directory must not be a symbolic link or reparse point; on Unix it must use owner-only `0700` permissions. |

`server-status` with `detail: Full` reports the effective agent-relevant non-sensitive configuration and all startup fallback warnings. Cache settings, plugin directories and the state-directory path are not included in that public configuration projection.

Code Action references are temporary, process-local and snapshot-bound. Increasing their lifetime does not make them portable across server restarts or Workspace revisions; follow the [Code Action workflow](CodeActions.md) and rediscover when directed.

The default state directory is `%LOCALAPPDATA%\roslyn-workbench-mcp\state` on Windows, `$XDG_STATE_HOME/roslyn-workbench-mcp` on Linux when `XDG_STATE_HOME` is an absolute path, `~/.local/state/roslyn-workbench-mcp` on Linux otherwise, and `~/Library/Application Support/roslyn-workbench-mcp/state` on macOS. Unix state directories are created with `0700` permissions and recovery files with `0600`; Windows state inherits the current user's local-application-data access controls.

The plugin set is discovered once during startup. Adding, removing or upgrading a plugin package requires a server restart.
