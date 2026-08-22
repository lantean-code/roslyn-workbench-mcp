namespace Roslyn.Workbench.Mcp.TestSupport;

internal sealed class MiniWorkspace : IDisposable
{
    private readonly AdhocWorkspace _workspace;
    private readonly IReadOnlyDictionary<string, DocumentId> _documentIdsByPath;

    public MiniWorkspace(
        AdhocWorkspace workspace,
        Solution solution,
        IReadOnlyDictionary<string, DocumentId> documentIdsByPath)
    {
        _workspace = workspace;
        Solution = solution;
        _documentIdsByPath = documentIdsByPath;
    }

    public Solution Solution { get; }

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

        var documentSelector = new DocumentSelector
        {
            Path = path,
        };

        var span = SelectorTestFactory.CreateTextSpanSelector(documentSelector, start, text.Length);

        return new LocationSelector
        {
            Span = span,
        };
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }
}
