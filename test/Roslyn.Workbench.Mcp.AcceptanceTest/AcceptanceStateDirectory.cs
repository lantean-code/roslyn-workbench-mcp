namespace Roslyn.Workbench.Mcp.AcceptanceTest;

internal static class AcceptanceStateDirectory
{
    private const UnixFileMode _privateDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private const UnixFileMode _privateFileMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private const UnixFileMode _broadDirectoryMode =
        _privateDirectoryMode
        | UnixFileMode.GroupRead
        | UnixFileMode.GroupExecute
        | UnixFileMode.OtherRead
        | UnixFileMode.OtherExecute;

    public static void Create(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path);
            return;
        }

        Directory.CreateDirectory(path, _privateDirectoryMode);
    }

    public static void Prepare(string path, AcceptanceStateDirectoryPreparation preparation)
    {
        if (preparation == AcceptanceStateDirectoryPreparation.Absent)
        {
            return;
        }

        if (preparation == AcceptanceStateDirectoryPreparation.BroadUnix
            && !OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path, _broadDirectoryMode);
            File.SetUnixFileMode(path, _broadDirectoryMode);
            return;
        }

        Create(path);
    }

    public static void MakeFilePrivate(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, _privateFileMode);
        }
    }
}
