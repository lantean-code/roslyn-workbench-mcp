namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

public sealed class TestWorkspaceFixture : IAsyncDisposable
{
    private readonly MaterializedWorkspaceAsset _asset;

    private TestWorkspaceFixture(
        MaterializedWorkspaceAsset asset,
        string projectPath,
        string documentPath,
        string directoryBuildPropsPath,
        string editorConfigPath,
        string? sharedDocumentPath = null)
    {
        _asset = asset;
        ProjectPath = Path.Combine(asset.WorkspaceRoot, projectPath);
        DocumentPath = Path.Combine(asset.WorkspaceRoot, documentPath);
        DirectoryBuildPropsPath = Path.Combine(asset.WorkspaceRoot, directoryBuildPropsPath);
        EditorConfigPath = Path.Combine(asset.WorkspaceRoot, editorConfigPath);
        SharedDocumentPath = sharedDocumentPath is null
            ? null
            : Path.Combine(asset.WorkspaceRoot, sharedDocumentPath);
    }

    public string DocumentPath { get; }

    public string DirectoryBuildPropsPath { get; }

    public string EditorConfigPath { get; }

    public string ProjectPath { get; }

    public string? SharedDocumentPath { get; }

    public string StateRoot
    {
        get
        {
            return _asset.StateRoot;
        }
    }

    public string WorkspaceRoot
    {
        get
        {
            return _asset.WorkspaceRoot;
        }
    }

    public static Task<TestWorkspaceFixture> CreateAsync()
    {
        return Task.FromResult(Create(
            "SdkProject",
            "Sample.csproj",
            "Class1.cs",
            "Directory.Build.props",
            ".editorconfig"));
    }

    public static Task<TestWorkspaceFixture> CreateLegacyProjectAsync()
    {
        return Task.FromResult(Create(
            "CompatibilitySamples/LegacyNet472",
            "Legacy.csproj",
            "Class1.cs",
            "Directory.Build.props",
            ".editorconfig"));
    }

    public static Task<TestWorkspaceFixture> CreateMalformedProjectAsync()
    {
        return Task.FromResult(Create(
            "CompatibilitySamples/MalformedSdkProject",
            "Broken.csproj",
            "Class1.cs",
            "Directory.Build.props",
            ".editorconfig"));
    }

    public static Task<TestWorkspaceFixture> CreateAmbiguousAsync()
    {
        return Task.FromResult(Create(
            "CompatibilitySamples/AmbiguousProjectGraph",
            "ProjectOne/Sample.csproj",
            "ProjectOne/Class1.cs",
            "Directory.Build.props",
            ".editorconfig",
            "Shared/SharedClass.cs"));
    }

    internal ComponentWorkspace CreateWorkspace()
    {
        return ComponentWorkspace.Create(new ComponentWorkspaceOptions
        {
            StateDirectory = StateRoot,
        });
    }

    public ValueTask DisposeAsync()
    {
        return _asset.DisposeAsync();
    }

    private static TestWorkspaceFixture Create(
        string templateName,
        string projectPath,
        string documentPath,
        string directoryBuildPropsPath,
        string editorConfigPath,
        string? sharedDocumentPath = null)
    {
        return new TestWorkspaceFixture(
            WorkspaceAssetMaterializer.Materialize(templateName),
            projectPath,
            documentPath,
            directoryBuildPropsPath,
            editorConfigPath,
            sharedDocumentPath);
    }
}
