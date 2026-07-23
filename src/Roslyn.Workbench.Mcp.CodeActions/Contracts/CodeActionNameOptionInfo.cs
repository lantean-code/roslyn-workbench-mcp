namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Describes a simple named option for a code action.
/// </summary>
internal sealed record CodeActionNameOptionInfo
{
    /// <summary>
    /// Gets the logical option name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the display label.
    /// </summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional default value.
    /// </summary>
    public string? DefaultValue { get; init; }
}
