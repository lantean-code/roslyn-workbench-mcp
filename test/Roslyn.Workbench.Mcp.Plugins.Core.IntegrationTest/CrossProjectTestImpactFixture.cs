namespace Roslyn.Workbench.Mcp.Plugins.Core.Test;

internal sealed class CrossProjectTestImpactFixture : IDisposable
{
    private readonly MaterializedWorkspaceAsset _asset;

    private CrossProjectTestImpactFixture(MaterializedWorkspaceAsset asset)
    {
        _asset = asset;
        SolutionPath = Path.Combine(asset.WorkspaceRoot, "Sample.slnx");
    }

    public string SolutionPath { get; }

    public static CrossProjectTestImpactFixture Create()
    {
        var asset = WorkspaceAssetMaterializer.Materialize("CrossProjectTestImpact");
        return new CrossProjectTestImpactFixture(asset);
    }

    public void Dispose()
    {
        _asset.Dispose();
    }
}
