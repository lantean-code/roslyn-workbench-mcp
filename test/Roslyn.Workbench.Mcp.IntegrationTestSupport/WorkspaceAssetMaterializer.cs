namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

public static class WorkspaceAssetMaterializer
{
    private const string DeletionManifestFileName = ".asset-delete";

    private static readonly HashSet<string> _excludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".vs",
        "bin",
        "obj",
        "recovery",
    };

    public static MaterializedWorkspaceAsset Materialize(string templateName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);

        var templateRoot = Path.Combine(AppContext.BaseDirectory, "TestAssets", "Workspaces", templateName);
        if (!Directory.Exists(templateRoot))
        {
            throw new DirectoryNotFoundException($"Workspace asset template '{templateName}' was not found at '{templateRoot}'.");
        }

        return MaterializeFromTemplateRoot(templateRoot);
    }

    public static MaterializedWorkspaceAsset MaterializeProfiled(string templateName, string profileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);

        var templateRoot = Path.Combine(AppContext.BaseDirectory, "TestAssets", "Workspaces", templateName, "Base");
        var profileRoot = Path.Combine(AppContext.BaseDirectory, "TestAssets", "Workspaces", templateName, "Profiles", profileName);
        if (!Directory.Exists(templateRoot))
        {
            throw new DirectoryNotFoundException($"Workspace asset template '{templateName}' was not found at '{templateRoot}'.");
        }

        if (!Directory.Exists(profileRoot))
        {
            throw new DirectoryNotFoundException($"Workspace asset profile '{profileName}' was not found at '{profileRoot}'.");
        }

        return MaterializeFromTemplateRoots(templateRoot, profileRoot);
    }

    internal static MaterializedWorkspaceAsset MaterializeFromTemplateRoot(string templateRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateRoot);

        var scenarioDirectory = TemporaryDirectory.Create("roslyn-workbench-mcp-assets");
        try
        {
            var workspaceRoot = Path.Combine(scenarioDirectory.DirectoryPath, "workspace");
            var stateRoot = Path.Combine(scenarioDirectory.DirectoryPath, "state");
            CopyDirectory(templateRoot, workspaceRoot, overwrite: false, skipDeletionManifest: false);
            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".git"));
            CreateStateDirectory(stateRoot);
            return new MaterializedWorkspaceAsset(scenarioDirectory, workspaceRoot, stateRoot);
        }
        catch
        {
            scenarioDirectory.Dispose();
            throw;
        }
    }

    internal static MaterializedWorkspaceAsset MaterializeFromTemplateRoots(string templateRoot, string profileRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileRoot);

        var scenarioDirectory = TemporaryDirectory.Create("roslyn-workbench-mcp-assets");
        try
        {
            var workspaceRoot = Path.Combine(scenarioDirectory.DirectoryPath, "workspace");
            var stateRoot = Path.Combine(scenarioDirectory.DirectoryPath, "state");
            CopyDirectory(templateRoot, workspaceRoot, overwrite: false, skipDeletionManifest: false);
            CopyDirectory(profileRoot, workspaceRoot, overwrite: true, skipDeletionManifest: true);
            ApplyDeletionManifest(profileRoot, workspaceRoot);
            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".git"));
            CreateStateDirectory(stateRoot);
            return new MaterializedWorkspaceAsset(scenarioDirectory, workspaceRoot, stateRoot);
        }
        catch
        {
            scenarioDirectory.Dispose();
            throw;
        }
    }

    private static void ApplyDeletionManifest(string profileRoot, string workspaceRoot)
    {
        var manifestPath = Path.Combine(profileRoot, DeletionManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return;
        }

        foreach (var relativePath in File.ReadAllLines(manifestPath).Where(static line => !string.IsNullOrWhiteSpace(line)))
        {
            var destinationPath = Path.GetFullPath(Path.Combine(workspaceRoot, relativePath));
            if (!destinationPath.StartsWith(workspaceRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Asset deletion path '{relativePath}' escapes workspace root '{workspaceRoot}'.");
            }

            File.Delete(destinationPath);
        }
    }

    private static void CopyDirectory(string sourceRoot, string destinationRoot, bool overwrite, bool skipDeletionManifest)
    {
        Directory.CreateDirectory(destinationRoot);

        foreach (var sourceFile in Directory.EnumerateFiles(sourceRoot))
        {
            if (skipDeletionManifest && Path.GetFileName(sourceFile).Equals(DeletionManifestFileName, StringComparison.Ordinal))
            {
                continue;
            }

            File.Copy(sourceFile, Path.Combine(destinationRoot, Path.GetFileName(sourceFile)), overwrite);
        }

        foreach (var sourceDirectory in Directory.EnumerateDirectories(sourceRoot))
        {
            if (_excludedDirectoryNames.Contains(Path.GetFileName(sourceDirectory)))
            {
                continue;
            }

            CopyDirectory(sourceDirectory, Path.Combine(destinationRoot, Path.GetFileName(sourceDirectory)), overwrite, skipDeletionManifest);
        }
    }

    private static void CreateStateDirectory(string stateRoot)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(stateRoot);
            return;
        }

        Directory.CreateDirectory(
            stateRoot,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
}
