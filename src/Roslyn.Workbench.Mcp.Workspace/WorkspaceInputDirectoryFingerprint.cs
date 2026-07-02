namespace Roslyn.Workbench.Mcp.Workspace;

internal sealed record WorkspaceInputDirectoryFingerprint
{
    public string Path { get; init; } = string.Empty;

    public DateTime LastWriteTimeUtc { get; init; }

    public static WorkspaceInputDirectoryFingerprint Create(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var normalizedPath = System.IO.Path.GetFullPath(path);
        var info = new DirectoryInfo(normalizedPath);

        return new WorkspaceInputDirectoryFingerprint
        {
            Path = normalizedPath,
            LastWriteTimeUtc = info.LastWriteTimeUtc,
        };
    }
}
