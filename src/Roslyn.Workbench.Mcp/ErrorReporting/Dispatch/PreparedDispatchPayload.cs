using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Dispatch;

internal abstract record PreparedDispatchPayload
{
    public required string DispatcherName { get; init; }

    public required string Destination { get; init; }

    public required string ReportId { get; init; }

    public required ExternalErrorReport Report { get; init; }

    public required ImmutableArray<byte> PreviewBytes { get; init; }

    public required string PreviewJson { get; init; }
}

internal sealed record PreparedDispatchPayload<TDispatchState> : PreparedDispatchPayload
{
    public required TDispatchState DispatchState { get; init; }
}
