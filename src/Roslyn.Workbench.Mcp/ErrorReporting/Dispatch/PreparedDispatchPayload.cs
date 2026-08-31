using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Dispatch;

/// <summary>
/// Holds the exact provider-specific payload presented for review together with the report used to create it.
/// </summary>
internal abstract record PreparedDispatchPayload
{
    /// <summary>
    /// Gets the provider that prepared the payload.
    /// </summary>
    public required string DispatcherName { get; init; }

    /// <summary>
    /// Gets the destination to which the provider will send the payload.
    /// </summary>
    public required string Destination { get; init; }

    /// <summary>
    /// Gets the immutable identifier of the projected error report.
    /// </summary>
    public required string ReportId { get; init; }

    /// <summary>
    /// Gets the projected report from which the provider payload was created.
    /// </summary>
    public required ExternalErrorReport Report { get; init; }

    /// <summary>
    /// Gets the exact serialized bytes used to calculate the review digest.
    /// </summary>
    public required ImmutableArray<byte> PreviewBytes { get; init; }

    /// <summary>
    /// Gets the JSON representation presented to the user for review.
    /// </summary>
    public required string PreviewJson { get; init; }
}

/// <summary>
/// Adds provider-specific dispatch state to a prepared review payload.
/// </summary>
/// <typeparam name="TDispatchState">The type of state required by the provider during dispatch.</typeparam>
internal sealed record PreparedDispatchPayload<TDispatchState> : PreparedDispatchPayload
{
    /// <summary>
    /// Gets the provider-specific state required to send the payload.
    /// </summary>
    public required TDispatchState DispatchState { get; init; }
}
