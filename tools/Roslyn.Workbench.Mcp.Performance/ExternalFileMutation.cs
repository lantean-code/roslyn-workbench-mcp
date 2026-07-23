namespace Roslyn.Workbench.Mcp.Performance;

internal sealed record ExternalFileMutation
{
    public required string Path { get; init; }

    public required string OriginalSha256 { get; init; }

    public required string ExternalSha256 { get; init; }

    public required long OriginalBytes { get; init; }

    public required long ExternalBytes { get; init; }
}
