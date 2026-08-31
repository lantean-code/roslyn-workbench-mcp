namespace Roslyn.Workbench.Mcp.Plugins.Configuration;

/// <summary>
/// Exposes configured tool metadata to preparation while retaining control of builder mutability.
/// </summary>
internal interface IToolConfigurationBuilderState
{
    /// <summary>
    /// Gets the configured protocol tool name, or <see langword="null"/> when it was not supplied.
    /// </summary>
    string? Name { get; }

    /// <summary>
    /// Gets the configured display title, or <see langword="null"/> when it was not supplied.
    /// </summary>
    string? Title { get; }

    /// <summary>
    /// Gets the configured agent-facing tool description, or <see langword="null"/> when it was not supplied.
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// Gets the configured result summary template, or <see langword="null"/> when it was not supplied.
    /// </summary>
    string? ResultSummary { get; }

    /// <summary>
    /// Gets the configured destructive-behaviour hint, or <see langword="null"/> when the tool family determines it.
    /// </summary>
    bool? Destructive { get; }

    /// <summary>
    /// Prevents further builder changes after plugin configuration completes.
    /// </summary>
    void Freeze();
}
