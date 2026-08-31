namespace Roslyn.Workbench.Mcp.CodeActions.Configuration;

/// <summary>
/// Configures Code Action reference retention and diagnostic context limits.
/// </summary>
internal sealed class CodeActionExecutionOptions
{
    /// <summary>
    /// Defines the default number of diagnostic contexts retained for one action.
    /// </summary>
    public const int DefaultMaximumDiagnosticContextsPerAction = 10;

    /// <summary>
    /// Gets or sets the maximum number of diagnostic contexts retained for one action.
    /// </summary>
    public int MaximumDiagnosticContextsPerAction { get; set; } = DefaultMaximumDiagnosticContextsPerAction;

    /// <summary>
    /// Gets or sets how long a discovered Code Action reference remains usable.
    /// </summary>
    public TimeSpan ReferenceLifetime { get; set; } = TimeSpan.FromMinutes(5);
}
