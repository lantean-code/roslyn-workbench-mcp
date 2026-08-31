using System.ComponentModel;

namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Describes a diagnostic associated with a discovered code fix.
/// </summary>
internal sealed record CodeActionDiagnosticContext
{
    /// <summary>
    /// The diagnostic identifier.
    /// </summary>
    [Description("The diagnostic identifier.")]
    public required string Id { get; init; }

    /// <summary>
    /// The diagnostic message.
    /// </summary>
    [Description("The diagnostic message.")]
    public required string Message { get; init; }
}
