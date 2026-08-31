using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Projection;

/// <summary>
/// Contains the externally eligible type, optional message and stack frames for one exception.
/// </summary>
internal sealed record ExternalException
{
    /// <summary>
    /// Gets the exception type, or a generic external-component classification when its identity is withheld.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the captured exception message when the user approved its inclusion.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Gets stack frames owned by Roslyn Workbench, Roslyn or .NET components.
    /// </summary>
    public ImmutableArray<ExternalStackFrame> StackFrames { get; init; } = [];
}
