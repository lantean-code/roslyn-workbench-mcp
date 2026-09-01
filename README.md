# Roslyn Workbench MCP

![Roslyn Workbench MCP wordmark](assets/roslyn-workbench-mcp-wordmark.svg)

Roslyn Workbench is a local stdio MCP server for inspecting C# workspaces and staging Roslyn-powered source changes through explicit transactions.

It provides:

- persistent multi-workspace sessions instead of reloading a solution for every tool call;
- Roslyn queries for symbols, references, diagnostics, dependencies, metrics and code structure;
- transactional mutation and Code Action workflows with preview, history, rollback, conflict detection and crash recovery; and
- bounded local diagnostics with optional reviewed, user-approved external error reporting through a pluggable dispatcher; and
- trusted in-process plugins for additional query and mutation tools.

## Documentation

Start with the [documentation site](https://lantean-code.github.io/roslyn-workbench-mcp/) for setup, configuration, tool discovery and safe workspace operation. The [site sources](docs/content/index.md) are kept with the code; historical plans, audits and implementation evidence remain separately under `docs/development`.

Security concerns must be reported privately through the process in [SECURITY.md](SECURITY.md). Development and pull-request guidance is available in [CONTRIBUTING.md](CONTRIBUTING.md).

Roslyn Workbench MCP is licensed under the [MIT License](LICENSE).

## External plugins

Pass one or more `--plugin-directory <search-root>` options when starting the server. Each immediate child directory beneath a search root is one plugin package; DLLs directly in the search root and recursively nested packages are ignored.

```text
plugins/
  example-tools/
    Example.Tools.dll
    Example.Dependency.dll
    Example.Tools.deps.json
```

The package must contain exactly one assembly with exactly one `RoslynPluginAttribute`. Plugin identity and compatibility come from assembly metadata, so no JSON manifest is required. See [Third-Party Plugin Authoring](docs/PluginAuthoring.md) for the public API and packaging rules.
