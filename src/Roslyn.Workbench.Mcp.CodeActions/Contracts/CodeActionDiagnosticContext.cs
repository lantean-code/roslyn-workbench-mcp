using System.ComponentModel;

namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Describes a diagnostic associated with a discovered code fix.
/// </summary>
internal sealed record CodeActionDiagnosticContext
{
    /// <summary>
    /// Gets the diagnostic identifier.
    /// </summary>
    [Description("The diagnostic identifier.")]
    public required string Id { get; init; }

    /// <summary>
    /// Gets the diagnostic message.
    /// </summary>
    [Description("The diagnostic message.")]
    public required string Message { get; init; }
}
