namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one callable overload signature.
/// </summary>
internal sealed record CallableSignature
{
    /// <summary>
    /// Gets the callable display name.
    /// </summary>
    [Description("The callable display name.")]
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the callable kind.
    /// </summary>
    [Description("The callable kind.")]
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// Gets the ordered parameters for the callable.
    /// </summary>
    [Description("The ordered parameters for the callable.")]
    public IReadOnlyList<ParameterInfo> Parameters { get; init; } = [];

    /// <summary>
    /// Gets the return type, when applicable.
    /// </summary>
    [Description("The return type, when applicable.")]
    public TypeInfo? ReturnType { get; init; }
}
