namespace Roslyn.Workbench.Mcp.Workspace.Loading;

internal sealed class WorkspaceMsBuildPropertiesResolver : IWorkspaceMsBuildPropertiesResolver
{
    private readonly IFileSystem _fileSystem;
    private readonly IWorkspacePathNormalizer _pathNormalizer;

    public WorkspaceMsBuildPropertiesResolver(
        IFileSystem fileSystem,
        IWorkspacePathNormalizer pathNormalizer)
    {
        _fileSystem = fileSystem;
        _pathNormalizer = pathNormalizer;
    }

    public WorkspaceMsBuildPropertiesResolution Resolve(WorkspaceMsBuildProperties? properties)
    {
        if (properties is null)
        {
            return WorkspaceMsBuildPropertiesResolution.Success(properties: null);
        }

        if (!TryNormalizeOptionalValue(properties.Configuration, out var configuration)
            || !TryNormalizeOptionalValue(properties.Platform, out var platform)
            || !TryNormalizeOptionalValue(properties.RuntimeIdentifier, out var runtimeIdentifier)
            || !TryNormalizeOptionalValue(properties.TargetFramework, out var targetFramework))
        {
            return CreateFailure("MSBuild property values must not be empty or whitespace.");
        }

        var artifactsPathResolution = ResolveArtifactsPath(properties.ArtifactsPath);
        if (artifactsPathResolution.HasError)
        {
            return artifactsPathResolution;
        }

        var resolvedProperties = new WorkspaceMsBuildProperties
        {
            ArtifactsPath = artifactsPathResolution.Properties?.ArtifactsPath,
            Configuration = configuration,
            Platform = platform,
            RuntimeIdentifier = runtimeIdentifier,
            TargetFramework = targetFramework,
        };

        return HasValues(resolvedProperties)
            ? WorkspaceMsBuildPropertiesResolution.Success(resolvedProperties)
            : WorkspaceMsBuildPropertiesResolution.Success(properties: null);
    }

    private WorkspaceMsBuildPropertiesResolution ResolveArtifactsPath(string? artifactsPath)
    {
        if (artifactsPath is null)
        {
            return WorkspaceMsBuildPropertiesResolution.Success(properties: null);
        }

        if (string.IsNullOrWhiteSpace(artifactsPath)
            || !_fileSystem.Path.IsPathFullyQualified(artifactsPath)
            || !_pathNormalizer.TryGetFullPath(artifactsPath, out var normalizedPath)
            || !_fileSystem.Directory.Exists(normalizedPath))
        {
            return CreateFailure("The MSBuild artifacts path must be an existing absolute directory.");
        }

        var properties = new WorkspaceMsBuildProperties
        {
            ArtifactsPath = normalizedPath,
        };

        return WorkspaceMsBuildPropertiesResolution.Success(properties);
    }

    private static bool HasValues(WorkspaceMsBuildProperties properties)
    {
        return properties.ArtifactsPath is not null
            || properties.Configuration is not null
            || properties.Platform is not null
            || properties.RuntimeIdentifier is not null
            || properties.TargetFramework is not null;
    }

    private static bool TryNormalizeOptionalValue(string? value, out string? normalizedValue)
    {
        normalizedValue = value?.Trim();
        return value is null || !string.IsNullOrWhiteSpace(normalizedValue);
    }

    private static WorkspaceMsBuildPropertiesResolution CreateFailure(string message)
    {
        var error = new WorkspaceOperationError
        {
            Code = "WorkspaceMsBuildPropertiesInvalid",
            Message = message,
        };

        return WorkspaceMsBuildPropertiesResolution.Failure(error);
    }
}
