using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;
using Roslyn.Workbench.Mcp.Workspace;

namespace Roslyn.Workbench.Mcp.TestSupport;

public sealed class MiniWorkspace : IDisposable
{
    private readonly AdhocWorkspace _workspace;
    private readonly string _loadedPath;
    private readonly IReadOnlyDictionary<string, DocumentId> _documentIdsByPath;

    internal MiniWorkspace(
        AdhocWorkspace workspace,
        Solution solution,
        string loadedPath,
        string projectPath,
        IReadOnlyDictionary<string, DocumentId> documentIdsByPath)
    {
        _workspace = workspace;
        Solution = solution;
        _loadedPath = loadedPath;
        ProjectPath = projectPath;
        _documentIdsByPath = documentIdsByPath;
    }

    public string ProjectPath { get; }

    public Solution Solution { get; }

    public WorkspaceIdentity CreateWorkspaceIdentity()
    {
        return new WorkspaceIdentity
        {
            WorkspaceId = "WorkspaceId",
            WorkspaceEpoch = 1,
            LoadedPath = _loadedPath,
        };
    }

    public IWorkspaceResolver CreateResolver(WorkspaceIdentity workspaceIdentity, int? transactionRevision = null)
    {
        ArgumentNullException.ThrowIfNull(workspaceIdentity);

        return new WorkspaceResolver(Solution, workspaceIdentity, transactionRevision);
    }

    public LocationSelector GetLocationSelector(string text, string? documentPath = null)
    {
        var path = documentPath ?? _documentIdsByPath.Keys.Single();
        if (!_documentIdsByPath.TryGetValue(path, out var documentId))
        {
            throw new InvalidOperationException($"The document '{path}' is not part of this workspace.");
        }

        var document = Solution.GetDocument(documentId) ?? throw new InvalidOperationException($"The document '{path}' could not be resolved.");
        var sourceText = document.GetTextAsync().GetAwaiter().GetResult().ToString();
        var start = sourceText.IndexOf(text, StringComparison.Ordinal);
        if (start < 0)
        {
            throw new InvalidOperationException($"The text '{text}' could not be found in '{path}'.");
        }

        return new LocationSelector
        {
            Span = new TextSpanSelector
            {
                Document = new DocumentSelector
                {
                    Path = path,
                },
                Start = start,
                Length = text.Length,
            },
        };
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }
}
