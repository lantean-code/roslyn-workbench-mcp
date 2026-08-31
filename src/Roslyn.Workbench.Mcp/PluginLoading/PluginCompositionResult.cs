using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Describes the result of plugin composition.
/// </summary>
internal sealed record PluginCompositionResult
{
    /// <summary>
    /// Gets a value indicating whether plugin composition completed without an error.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool Succeeded => Error is null;

    /// <summary>
    /// Gets the error reported when plugin composition fails.
    /// </summary>
    public string? Error { get; }

    private PluginCompositionResult(string? error)
    {
        Error = error;
    }

    /// <summary>
    /// Creates a result that represents successful completion.
    /// </summary>
    /// <returns>A result that represents successful completion.</returns>
    public static PluginCompositionResult Success()
    {
        return new PluginCompositionResult(error: null);
    }

    /// <summary>
    /// Creates a result that represents failure.
    /// </summary>
    /// <param name="error">The error that caused the operation to fail.</param>
    /// <returns>A result that represents failure.</returns>
    public static PluginCompositionResult Failure(string error)
    {
        return new PluginCompositionResult(error);
    }
}
