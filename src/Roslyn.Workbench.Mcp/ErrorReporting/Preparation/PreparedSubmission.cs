namespace Roslyn.Workbench.Mcp.ErrorReporting.Preparation;

internal sealed record PreparedSubmission
{
    public required string Handle { get; init; }

    public required Guid CorrelationId { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public required PreparedDispatchPayload Payload { get; init; }

    public string? WorkspaceId { get; init; }

    public long? WorkspaceEpoch { get; init; }

    public PreparedSubmissionState State { get; init; }

    public ErrorSubmissionReceipt? Receipt { get; init; }
}
