# roslyn-workbench-mcp

Roslyn MCP server with a transaction-based workspace model for safe local
analysis and refactoring.

### Next

IReplayCodeActionExecutor takes a nullable selection parameter only to throw if selection is null. Tracing the call stack up to the request using AddAwaitTool as an example AddAwaitRequest allows Selection to be null. If the calling code don't allow for nulls then the request shouldn't - this provides the wrong hint to the agent.
What are these 'Unavailable...' service implementations. They feel wrong. Shouldn't logic flow decide if something is unavailable not an implementation type?
