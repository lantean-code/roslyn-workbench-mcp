# roslyn-workbench-mcp

Roslyn MCP server with a transaction-based workspace model for safe local
analysis and refactoring.

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

### Next

IReplayCodeActionExecutor takes a nullable selection parameter only to throw if selection is null. Tracing the call stack up to the request using AddAwaitTool as an example AddAwaitRequest allows Selection to be null. If the calling code don't allow for nulls then the request shouldn't - this provides the wrong hint to the agent.
