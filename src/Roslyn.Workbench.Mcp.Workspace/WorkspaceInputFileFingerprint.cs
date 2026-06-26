namespace Roslyn.Workbench.Mcp.Workspace;

internal sealed record WorkspaceInputFileFingerprint
{
    public string Path { get; init; } = string.Empty;

    public DateTime LastWriteTimeUtc { get; init; }

    public long Length { get; init; }

    public static WorkspaceInputFileFingerprint Create(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var normalizedPath = System.IO.Path.GetFullPath(path);
        var info = new FileInfo(normalizedPath);

        return new WorkspaceInputFileFingerprint
        {
            Path = normalizedPath,
            LastWriteTimeUtc = info.LastWriteTimeUtc,
            Length = info.Length,
        };
    }
}
