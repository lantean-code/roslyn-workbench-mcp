namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

public sealed class SolutionHierarchyFixture : IAsyncDisposable
{
    private readonly MaterializedWorkspaceAsset _asset;

    private SolutionHierarchyFixture(MaterializedWorkspaceAsset asset)
    {
        _asset = asset;
        SolutionPath = Path.Combine(asset.WorkspaceRoot, "Sample.slnx");
    }

    public string SolutionPath { get; }

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

    public static Task<SolutionHierarchyFixture> CreateAsync()
    {
        return Task.FromResult(new SolutionHierarchyFixture(
            WorkspaceAssetMaterializer.Materialize("SolutionHierarchy")));
    }

    public ValueTask DisposeAsync()
    {
        return _asset.DisposeAsync();
    }
}
