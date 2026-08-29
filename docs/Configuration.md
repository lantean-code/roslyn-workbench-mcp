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

At startup, the Host verifies that the recovery directory supports exclusive file creation, durable writes and deletion. Startup fails with an actionable configuration error if the selected state directory cannot support recovery data; commit retains its own validation because filesystem permissions and availability can change while the Host is running.
| `--error-reporting-consent` | None | `prompt` | Exact case-sensitive `never`, `prompt` or `always`. `never` and `always` must be supplied explicitly on the command line; the similarly named environment variable is ignored and reported as a fallback warning. Invalid input fails closed to `never`. |
| `--error-record-capacity` | `ROSLYN_WORKBENCH_MCP_ERROR_RECORD_CAPACITY` | `100` | Temporary correlated local error records; supported range `10`–`1000`. |
| `--error-record-lifetime` | `ROSLYN_WORKBENCH_MCP_ERROR_RECORD_LIFETIME` | `01:00:00` | Absolute local error-record lifetime, up to 24 hours. |
| `--error-record-max-bytes` | `ROSLYN_WORKBENCH_MCP_ERROR_RECORD_MAX_BYTES` | `65536` | Maximum captured local record size; supported range `16384`–`262144` bytes. |
| `--error-submission-capacity` | `ROSLYN_WORKBENCH_MCP_ERROR_SUBMISSION_CAPACITY` | `50` | Temporary prepared-submission records; supported range `5`–`500`. |
| `--error-submission-lifetime` | `ROSLYN_WORKBENCH_MCP_ERROR_SUBMISSION_LIFETIME` | `00:30:00` | Absolute prepared-submission lifetime, up to four hours. |
| `--error-report-max-bytes` | `ROSLYN_WORKBENCH_MCP_ERROR_REPORT_MAX_BYTES` | `65536` | Maximum canonical external payload size; supported range `8192`–`262144` bytes. |

`server-status` with `detail: Full` reports the effective agent-relevant non-sensitive configuration and all startup fallback warnings. Its error-reporting projection includes only the built-in provider name, configured consent mode and effective configured consent state: `Disabled` for `never`, `PromptRequired` for `prompt` or `AlwaysApproved` for `always`. It never exposes the application-owned DSN or public submission key. Cache settings, plugin directories and the state-directory path are not included in that public configuration projection.

The Host includes its external error-report provider as application configuration. With consent set to `never`, the Host retains local correlated diagnostics but does not publish preparation or submission tools. `always` bypasses a consent prompt only: it never creates background traffic, and callers must still prepare, review and explicitly submit each report. See [Error reporting and privacy](ErrorReporting.md) for the complete workflow and data boundary.

## Build-time error-report provider

`ROSLYN_WORKBENCH_SENTRY_DSN` is a build-time environment variable, not a Host startup option. When it contains a valid HTTPS public-key Sentry DSN, MSBuild embeds the value in the Host assembly and the application selects the isolated Sentry SDK dispatcher. When it is absent or empty, the Host selects the logging dispatcher and writes only explicitly approved sanitised reports to stderr. A fork therefore does not inherit the maintainer's Sentry project from source.

For a local Bash build:

```bash
ROSLYN_WORKBENCH_SENTRY_DSN='https://public-key@sentry-host/project-id' \
  dotnet publish src/Roslyn.Workbench.Mcp/Roslyn.Workbench.Mcp.csproj --configuration Release
```

For GitHub Actions, store the DSN as the `SENTRY_DSN` Actions secret and expose it only to the step that compiles the Host:

```yaml
- name: Build
  env:
    ROSLYN_WORKBENCH_SENTRY_DSN: ${{ secrets.SENTRY_DSN }}
  run: dotnet build --configuration Release --no-restore
```

If a later publish uses `--no-build`, the variable belongs on the earlier build step because that compilation creates the embedded attribute. The DSN is recoverable from a published binary and must remain a public submission DSN rather than private provider credentials.

Code Action references are temporary, process-local and snapshot-bound. Increasing their lifetime does not make them portable across server restarts or Workspace revisions; follow the [Code Action workflow](CodeActions.md) and rediscover when directed.

The default state directory is `%LOCALAPPDATA%\roslyn-workbench-mcp\state` on Windows, `$XDG_STATE_HOME/roslyn-workbench-mcp` on Linux when `XDG_STATE_HOME` is an absolute path, `~/.local/state/roslyn-workbench-mcp` on Linux otherwise, and `~/Library/Application Support/roslyn-workbench-mcp/state` on macOS. Unix state directories are created with `0700` permissions and recovery files with `0600`; Windows state inherits the current user's local-application-data access controls.

The plugin set is discovered once during startup. Adding, removing or upgrading a plugin package requires a server restart.
