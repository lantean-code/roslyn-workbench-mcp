using System.Collections.Immutable;
using System.Text.Json;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Dispatch;

internal sealed record PreparedDispatchPayload
{
    public required string DispatcherName { get; init; }

    public required string Destination { get; init; }

    public required string ReportId { get; init; }

    public required ExternalErrorReport Report { get; init; }

    public required ImmutableArray<byte> PreviewBytes { get; init; }

    public required JsonElement Preview { get; init; }
}
