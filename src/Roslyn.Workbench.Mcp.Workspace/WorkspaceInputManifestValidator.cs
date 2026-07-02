namespace Roslyn.Workbench.Mcp.Workspace;

internal static class WorkspaceInputManifestValidator
{
    public static bool HasChanged(WorkspaceInputManifest? manifest, CancellationToken cancellationToken)
    {
        if (manifest is null)
        {
            return false;
        }

        foreach (var directory in manifest.Directories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Directory.Exists(directory.Path))
            {
                return true;
            }

            var info = new DirectoryInfo(directory.Path);
            if (info.LastWriteTimeUtc != directory.LastWriteTimeUtc)
            {
                return true;
            }
        }

        foreach (var file in manifest.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(file.Path))
            {
                return true;
            }

            var info = new FileInfo(file.Path);
            if (info.LastWriteTimeUtc != file.LastWriteTimeUtc || info.Length != file.Length)
            {
                return true;
            }
        }

        return false;
    }
}
