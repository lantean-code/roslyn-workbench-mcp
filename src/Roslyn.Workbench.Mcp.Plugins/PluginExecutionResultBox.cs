using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Plugins;

public sealed record PluginExecutionResultBox
{
    public ToolOutcome Outcome { get; init; }

    public object? Data { get; init; }

    public ChangeSummary? Changes { get; init; }

    public IReadOnlyList<DiagnosticInfo> Diagnostics { get; init; } = [];

    public IReadOnlyList<WarningInfo> Warnings { get; init; } = [];

    public ToolError? Error { get; init; }

    public RequiredAction? RequiredAction { get; init; }
}
