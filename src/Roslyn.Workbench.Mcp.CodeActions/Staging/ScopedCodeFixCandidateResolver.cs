namespace Roslyn.Workbench.Mcp.CodeActions.Staging;

internal sealed class ScopedCodeFixCandidateResolver : IScopedCodeFixCandidateResolver
{
    private readonly ICodeActionDiscoveryService _discoveryService;
    private readonly ICodeActionDiagnosticService _diagnosticService;

    public ScopedCodeFixCandidateResolver(
        ICodeActionDiscoveryService discoveryService,
        ICodeActionDiagnosticService diagnosticService)
    {
        _discoveryService = discoveryService;
        _diagnosticService = diagnosticService;
    }

    public async ValueTask<ScopedCodeFixCandidateResolution> ResolveAsync(
        ScopedCodeFixRequest request,
        IReadOnlyList<Document> documents,
        IWorkspaceResolver workspaceResolver,
        CancellationToken cancellationToken)
    {
        var matchingProviders = _discoveryService.GetMatchingCodeFixProviders(request.ProviderId);
        if (matchingProviders.Count == 0)
        {
            return ScopedCodeFixCandidateResolution.Unavailable(
                "No matching code-fix provider is available.");
        }

        var orderedDocuments = new List<(Document Document, string NormalizedPath)>(documents.Count);
        foreach (var document in documents)
        {
            var normalizedPath = workspaceResolver.NormalizeDocumentPath(document.FilePath ?? document.Name);
            orderedDocuments.Add((document, normalizedPath));
        }

        orderedDocuments.Sort(static (left, right) => StringComparer.Ordinal.Compare(
            left.NormalizedPath,
            right.NormalizedPath));

        var candidates = new List<ScopedCodeFixCandidate>();
        var candidateIdentities = new HashSet<CodeActionCandidateIdentity>();
        var hadDiagnostics = false;
        foreach (var (document, _) in orderedDocuments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var diagnostics = await _diagnosticService.GetScopedCodeFixDiagnosticsAsync(
                document,
                request.DiagnosticIds,
                request.AnalyzerTypeName,
                request.SyntheticDiagnosticId,
                cancellationToken);

            if (diagnostics.IsDefaultOrEmpty)
            {
                continue;
            }

            hadDiagnostics = true;
            var sourceText = await document.GetTextAsync(cancellationToken);
            var documentSpan = new TextSpan(0, sourceText.Length);

            foreach (var provider in matchingProviders)
            {
                var actions = await _discoveryService.DiscoverCodeFixesAsync(
                    provider,
                    document,
                    diagnostics,
                    cancellationToken);

                foreach (var action in actions)
                {
                    if (!MatchesRequest(action, request))
                    {
                        continue;
                    }

                    var identity = new CodeActionCandidateIdentity(
                        _discoveryService.GetProviderId(provider),
                        action.Title,
                        action.EquivalenceKey,
                        diagnosticIds: action.DiagnosticIds);

                    if (!candidateIdentities.Add(identity))
                    {
                        continue;
                    }

                    candidates.Add(new ScopedCodeFixCandidate
                    {
                        Document = document,
                        DocumentSpan = documentSpan,
                        Provider = provider,
                        Title = action.Title,
                        EquivalenceKey = action.EquivalenceKey,
                        DiagnosticIds = action.DiagnosticIds,
                    });
                }
            }
        }

        if (!hadDiagnostics)
        {
            return ScopedCodeFixCandidateResolution.NoDiagnostics();
        }

        if (candidates.Count == 0)
        {
            return ScopedCodeFixCandidateResolution.Unavailable(
                "No matching code fix was available for the selected scope.");
        }

        if (candidates.Count > 1)
        {
            return ScopedCodeFixCandidateResolution.Ambiguous(
                "The requested code fix could not be selected uniquely.");
        }

        return ScopedCodeFixCandidateResolution.Resolved(candidates[0]);
    }

    private static bool MatchesRequest(
        DiscoveredCodeAction action,
        ScopedCodeFixRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Title)
            && !string.Equals(action.Title, request.Title, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.EquivalenceKey))
        {
            return true;
        }

        return string.Equals(action.EquivalenceKey, request.EquivalenceKey, StringComparison.Ordinal);
    }
}
