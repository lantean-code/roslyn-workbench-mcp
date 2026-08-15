namespace Roslyn.Workbench.Mcp.PluginLoading;

internal sealed class PluginPackageDiscovery : IPluginPackageDiscovery
{
    private readonly IFileSystem _fileSystem;
    private readonly IPluginAssemblyMetadataReader _metadataReader;
    private readonly IPluginPackagePathPolicy _packagePathPolicy;

    public PluginPackageDiscovery(
        IFileSystem fileSystem,
        IPluginAssemblyMetadataReader metadataReader,
        IPluginPackagePathPolicy packagePathPolicy)
    {
        _fileSystem = fileSystem;
        _metadataReader = metadataReader;
        _packagePathPolicy = packagePathPolicy;
    }

    public IReadOnlyList<PluginPackageDiscoveryResult> Discover(IReadOnlyList<string> searchRoots)
    {
        var packageDirectories = new HashSet<FileSystemPathKey>();
        var results = new List<PluginPackageDiscoveryResult>();
        foreach (var searchRoot in searchRoots)
        {
            if (!_fileSystem.Directory.Exists(searchRoot))
            {
                continue;
            }

            try
            {
                foreach (var packageDirectory in _fileSystem.Directory.EnumerateDirectories(searchRoot))
                {
                    if (_packagePathPolicy.TryGetContainedPath(searchRoot, packageDirectory, out var containedPackageDirectory))
                    {
                        packageDirectories.Add(_packagePathPolicy.CreateKey(containedPackageDirectory));
                    }
                    else
                    {
                        results.Add(Disabled(
                            _fileSystem.Path.GetFileName(packageDirectory),
                            "Plugin package directory resolves outside its configured search root."));
                    }
                }
            }
            catch (Exception exception) when (IsDiscoveryException(exception))
            {
                results.Add(Disabled(
                    _fileSystem.Path.GetFileName(searchRoot),
                    $"Plugin search root could not be enumerated because {exception.GetType().Name} was raised."));
            }
        }

        foreach (var packageDirectory in packageDirectories.OrderBy(static key => key.Path, StringComparer.Ordinal))
        {
            results.Add(DiscoverPackage(packageDirectory.Path));
        }

        return results;
    }

    private PluginPackageDiscoveryResult DiscoverPackage(string packageDirectory)
    {
        var fallbackIdentity = _fileSystem.Path.GetFileName(packageDirectory);
        var markedAssemblies = new List<(string Path, PluginEntryPointMetadata EntryPoint)>();

        try
        {
            var assemblyPaths = _fileSystem.Directory
                .EnumerateFiles(packageDirectory, "*.dll", SearchOption.TopDirectoryOnly)
                .OrderBy(static path => path, StringComparer.Ordinal)
                .ToArray();

            foreach (var assemblyPath in assemblyPaths)
            {
                if (!_packagePathPolicy.TryGetContainedPath(packageDirectory, assemblyPath, out var containedAssemblyPath))
                {
                    return Disabled(fallbackIdentity, "Plugin assembly resolves outside its package directory.");
                }

                var inspection = _metadataReader.Inspect(containedAssemblyPath);
                if (inspection.Failed)
                {
                    return Disabled(fallbackIdentity, $"Assembly '{_fileSystem.Path.GetFileName(containedAssemblyPath)}' has malformed metadata: {inspection.Error}");
                }

                if (!inspection.Succeeded)
                {
                    continue;
                }

                foreach (var entryPoint in inspection.EntryPoints)
                {
                    markedAssemblies.Add((containedAssemblyPath, entryPoint));
                }
            }
        }
        catch (Exception exception) when (IsDiscoveryException(exception))
        {
            return Disabled(fallbackIdentity, $"Plugin package could not be enumerated because {exception.GetType().Name} was raised.");
        }

        if (markedAssemblies.Count != 1)
        {
            return Disabled(
                fallbackIdentity,
                markedAssemblies.Count == 0
                    ? "Plugin package does not contain a RoslynPlugin entry assembly."
                    : "Plugin package contains multiple RoslynPlugin entry points.");
        }

        var markedAssembly = markedAssemblies[0];
        var candidate = new PluginPackageCandidate
        {
            PackageDirectory = packageDirectory,
            EntryAssemblyPath = markedAssembly.Path,
            EntryPoint = markedAssembly.EntryPoint,
        };

        return new PluginPackageDiscoveryResult
        {
            FallbackIdentity = fallbackIdentity,
            Candidate = candidate,
        };
    }

    private static PluginPackageDiscoveryResult Disabled(string fallbackIdentity, string error)
    {
        return new PluginPackageDiscoveryResult
        {
            FallbackIdentity = fallbackIdentity,
            Error = error,
        };
    }

    private static bool IsDiscoveryException(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException;
    }
}
