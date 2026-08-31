namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

/// <summary>
/// Describes an exception isolated at a Code Action provider boundary.
/// </summary>
internal sealed record CodeActionProviderFailure
{
    /// <summary>
    /// Gets the stable identifier of the failing provider.
    /// </summary>
    public required string ProviderId { get; init; }

    /// <summary>
    /// Gets the provider operation that failed.
    /// </summary>
    public required string Operation { get; init; }

    /// <summary>
    /// Gets the exception type without exposing its potentially sensitive message.
    /// </summary>
    public required string ExceptionType { get; init; }
}
