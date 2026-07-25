# Configuration

Roslyn Workbench accepts command-line options and equivalent environment variables. A command-line scalar takes precedence over its environment variable; when a scalar option appears more than once, the last value wins. Invalid values fall back to the documented default and are reported as `StartupConfigurationFallback` warnings.

| Command-line option | Environment variable | Default | Meaning |
| --- | --- | --: | --- |
| `--plugin-directory` | `ROSLYN_WORKBENCH_MCP_PLUGIN_DIRECTORY` | None | Adds plugin search roots. The option is repeatable; the environment value uses the platform path separator. Command-line and environment roots are combined and deduplicated. |
| `--default-max-results` | `ROSLYN_WORKBENCH_MCP_DEFAULT_MAX_RESULTS` | `100` | Positive Host compatibility baseline for third-party tools that have not established a curated request default. |
| `--code-action-token-lifetime` | `ROSLYN_WORKBENCH_MCP_CODE_ACTION_TOKEN_LIFETIME` | `00:05:00` | Positive invariant-culture `TimeSpan` controlling discovered Code Action token lifetime, up to `1.00:00:00` (24 hours). |
| `--max-transaction-revisions` | `ROSLYN_WORKBENCH_MCP_MAX_TRANSACTION_REVISIONS` | `20` | Positive maximum number of retained staged transaction revisions. |
| `--max-concurrent-queries` | `ROSLYN_WORKBENCH_MCP_MAX_CONCURRENT_QUERIES` | `2` | Positive maximum number of concurrent query leases. |
| `--tool-output-schema-mode` | `ROSLYN_WORKBENCH_MCP_TOOL_OUTPUT_SCHEMA_MODE` | `Omit` | `Omit` keeps `tools/list` compact; `Full` publishes generated family-specific output schemas. |
| `--state-directory` | `ROSLYN_WORKBENCH_MCP_STATE_DIRECTORY` | Per-user application state directory | Absolute or relative writable location for Host state and durable commit-recovery records. The directory must not be a symbolic link or reparse point; on Unix it must use owner-only `0700` permissions. |

`server-status` with `detail: Full` reports the effective non-sensitive configuration and all startup fallback warnings. Plugin directories and the state-directory path are not included in that public configuration projection.

The default state directory is `%LOCALAPPDATA%\roslyn-workbench-mcp\state` on Windows, `$XDG_STATE_HOME/roslyn-workbench-mcp` on Linux when `XDG_STATE_HOME` is an absolute path, `~/.local/state/roslyn-workbench-mcp` on Linux otherwise, and `~/Library/Application Support/roslyn-workbench-mcp/state` on macOS. Unix state directories are created with `0700` permissions and recovery files with `0600`; Windows state inherits the current user's local-application-data access controls.

The plugin set is discovered once during startup. Adding, removing or upgrading a plugin package requires a server restart.
