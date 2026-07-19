namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

public sealed class SolutionHierarchyFixture : IDisposable
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

    public static SolutionHierarchyFixture Create()
    {
        return new SolutionHierarchyFixture(
            WorkspaceAssetMaterializer.Materialize("SolutionHierarchy"));
    }

    public void Dispose()
    {
        _asset.Dispose();
    }
}
