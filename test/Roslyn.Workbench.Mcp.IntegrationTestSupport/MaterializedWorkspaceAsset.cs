namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

public sealed class MaterializedWorkspaceAsset : IDisposable
{
    private readonly TemporaryDirectory _scenarioDirectory;

    internal MaterializedWorkspaceAsset(
        TemporaryDirectory scenarioDirectory,
        string workspaceRoot,
        string stateRoot)
    {
        _scenarioDirectory = scenarioDirectory;
        WorkspaceRoot = workspaceRoot;
        StateRoot = stateRoot;
    }

    public string StateRoot { get; }

    public string WorkspaceRoot { get; }

    public void Dispose()
    {
        _scenarioDirectory.Dispose();
    }
}
