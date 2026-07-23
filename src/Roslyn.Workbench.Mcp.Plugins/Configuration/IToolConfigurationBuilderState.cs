namespace Roslyn.Workbench.Mcp.Plugins.Configuration;

internal interface IToolConfigurationBuilderState
{
    string? Name { get; }

    string? Title { get; }

    string? Description { get; }

    string? ResultSummary { get; }

    bool? Destructive { get; }

    void Freeze();
}
