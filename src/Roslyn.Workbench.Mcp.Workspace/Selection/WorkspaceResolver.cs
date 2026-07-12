using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Workspace.Selection;

internal sealed class WorkspaceResolver : IWorkspaceResolver
{
    private readonly Solution _solution;
    private readonly WorkspaceIdentity? _workspaceIdentity;
    private readonly int? _transactionRevision;
    private readonly string _workspaceRoot;

    public WorkspaceResolver(Solution solution, WorkspaceIdentity? workspaceIdentity, int? transactionRevision)
    {
        _solution = solution;
        _workspaceIdentity = workspaceIdentity;
        _transactionRevision = transactionRevision;
        _workspaceRoot = workspaceIdentity is null
            ? string.Empty
            : Path.GetDirectoryName(workspaceIdentity.LoadedPath) ?? string.Empty;
    }

    public ResolvedLocation? CreateResolvedLocation(Location location)
    {
        if (!location.IsInSource || location.SourceTree is null || _workspaceIdentity is null)
        {
            return null;
        }

        var text = location.SourceTree.GetText();
        var span = location.SourceSpan;
        var linePosition = text.Lines.GetLinePosition(span.Start);
        var document = _solution.GetDocument(location.SourceTree);

        return new ResolvedLocation
        {
            WorkspaceId = _workspaceIdentity.WorkspaceId,
            Document = document is null ? null : CreateDocumentReference(document),
            Span = new TextSpanRange
            {
                Start = span.Start,
                Length = span.Length,
            },
            Line = linePosition.Line + 1,
            Column = linePosition.Character + 1,
            WorkspaceEpoch = _workspaceIdentity.WorkspaceEpoch,
            TransactionRevision = _transactionRevision,
        };
    }

    public DocumentReference? CreateDocumentReference(Document document)
    {

        return new DocumentReference
        {
            DocumentId = document.Id.Id.ToString(),
            ProjectId = document.Project.Id.Id.ToString(),
            Path = NormalizeDocumentPath(document.FilePath ?? string.Empty),
        };
    }

    public SymbolReference CreateSymbolReference(ISymbol symbol)
    {

        var sourceLocation = symbol.Locations.FirstOrDefault(static location => location.IsInSource);

        return new SymbolReference
        {
            DisplayName = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            Kind = symbol.Kind.ToString(),
            DocumentationCommentId = symbol.GetDocumentationCommentId(),
            Location = sourceLocation is null ? null : CreateResolvedLocation(sourceLocation),
        };
    }

    public SnapshotMatchResult ValidateSnapshot(SnapshotPrecondition? precondition)
    {
        if (precondition is null)
        {
            return SnapshotMatchResult.Matched();
        }

        if (_workspaceIdentity is null)
        {
            return SnapshotMatchResult.WorkspaceEpochMismatch();
        }

        if (!string.IsNullOrWhiteSpace(precondition.WorkspaceId)
            && !string.Equals(precondition.WorkspaceId, _workspaceIdentity.WorkspaceId, StringComparison.Ordinal))
        {
            return SnapshotMatchResult.WorkspaceEpochMismatch();
        }

        if (precondition.WorkspaceEpoch != _workspaceIdentity.WorkspaceEpoch)
        {
            return SnapshotMatchResult.WorkspaceEpochMismatch();
        }

        if (precondition.TransactionRevision != _transactionRevision)
        {
            return SnapshotMatchResult.TransactionRevisionMismatch();
        }

        return SnapshotMatchResult.Matched();
    }

    public string NormalizeDocumentPath(string path)
    {
        return NormalizePath(path);
    }

    public string NormalizeProjectPath(string path)
    {
        return NormalizePath(path);
    }

    public SelectorResolveResult<Document> ResolveDocument(DocumentSelector selector)
    {

        if (!string.IsNullOrWhiteSpace(selector.DocumentId)
            && Guid.TryParse(selector.DocumentId, out var documentGuid))
        {
            var matchesById = _solution.Projects.SelectMany(static project => project.Documents).Where(document => document.Id.Id == documentGuid).ToArray();
            if (matchesById.Length == 1)
            {
                return SelectorResolveResult<Document>.Resolved(matchesById[0]);
            }

            if (matchesById.Length > 1)
            {
                return SelectorResolveResult<Document>.Ambiguous();
            }
        }

        if (string.IsNullOrWhiteSpace(selector.Path))
        {
            return SelectorResolveResult<Document>.NotFound();
        }

        var normalizedPath = NormalizeDocumentPath(selector.Path);
        var matches = _solution.Projects.SelectMany(static project => project.Documents)
            .Where(document => string.Equals(NormalizeDocumentPath(document.FilePath ?? string.Empty), normalizedPath, StringComparison.Ordinal))
            .ToArray();

        return matches.Length switch
        {
            1 => SelectorResolveResult<Document>.Resolved(matches[0]),
            > 1 => SelectorResolveResult<Document>.Ambiguous(),
            _ => SelectorResolveResult<Document>.NotFound(),
        };
    }

    public async ValueTask<SelectorResolveResult<Location>> ResolveLocationAsync(LocationSelector selector, CancellationToken cancellationToken)
    {

        var resolution = await ResolveDocumentSpanAsync(selector, cancellationToken).ConfigureAwait(false);
        if (resolution.IsResolved)
        {
            var syntaxTree = await resolution.Value.Document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
            return syntaxTree is null
                ? SelectorResolveResult<Location>.NotFound()
                : SelectorResolveResult<Location>.Resolved(syntaxTree.GetLocation(resolution.Value.Span));
        }

        return resolution.Status == SelectorResolveStatus.Ambiguous
            ? SelectorResolveResult<Location>.Ambiguous()
            : SelectorResolveResult<Location>.NotFound();
    }

    public SelectorResolveResult<Project> ResolveProject(ProjectSelector selector)
    {

        var matches = _solution.Projects.Where(project => MatchesProjectSelector(project, selector)).ToArray();
        return matches.Length switch
        {
            1 => SelectorResolveResult<Project>.Resolved(matches[0]),
            > 1 => SelectorResolveResult<Project>.Ambiguous(),
            _ => SelectorResolveResult<Project>.NotFound(),
        };
    }

    public async ValueTask<SelectorResolveResult<ISymbol>> ResolveSymbolAsync(SymbolSelector selector, CancellationToken cancellationToken)
    {

        if (!string.IsNullOrWhiteSpace(selector.DocumentationCommentId))
        {
            return await ResolveSymbolByDocumentationCommentIdAsync(selector.DocumentationCommentId, cancellationToken).ConfigureAwait(false);
        }

        if (selector.Location is null)
        {
            return SelectorResolveResult<ISymbol>.NotFound();
        }

        var locationResolution = await ResolveDocumentSpanAsync(selector.Location, cancellationToken).ConfigureAwait(false);
        if (!locationResolution.IsResolved)
        {
            return locationResolution.Status == SelectorResolveStatus.Ambiguous
                ? SelectorResolveResult<ISymbol>.Ambiguous()
                : SelectorResolveResult<ISymbol>.NotFound();
        }

        var document = locationResolution.Value.Document;
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return SelectorResolveResult<ISymbol>.NotFound();
        }

        var symbol = await SymbolFinder.FindSymbolAtPositionAsync(semanticModel, locationResolution.Value.Span.Start, _solution.Workspace, cancellationToken).ConfigureAwait(false);
        if (symbol is not null)
        {
            return SelectorResolveResult<ISymbol>.Resolved(symbol);
        }

        var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var node = syntaxRoot?.FindNode(locationResolution.Value.Span, getInnermostNodeForTie: true);
        symbol = node is null ? null : semanticModel.GetDeclaredSymbol(node) ?? semanticModel.GetSymbolInfo(node).Symbol;

        return symbol is null
            ? SelectorResolveResult<ISymbol>.NotFound()
            : SelectorResolveResult<ISymbol>.Resolved(symbol);
    }

    private bool MatchesProjectSelector(Project project, ProjectSelector selector)
    {
        var idMatches = string.IsNullOrWhiteSpace(selector.ProjectId)
            || string.Equals(project.Id.Id.ToString(), selector.ProjectId, StringComparison.OrdinalIgnoreCase);
        var nameMatches = string.IsNullOrWhiteSpace(selector.Name)
            || string.Equals(project.Name, selector.Name, StringComparison.Ordinal);
        var pathMatches = string.IsNullOrWhiteSpace(selector.Path)
            || string.Equals(
                NormalizeProjectPath(project.FilePath ?? string.Empty),
                NormalizeProjectPath(selector.Path),
                StringComparison.Ordinal);

        return idMatches && nameMatches && pathMatches;
    }

    private ValueTask<SelectorResolveResult<ResolvedDocumentSpan>> ResolveDocumentSpanAsync(LocationSelector selector, CancellationToken cancellationToken)
    {
        if (selector.Span is not null)
        {
            return ResolveTextSpanAsync(selector.Span, cancellationToken);
        }

        if (selector.Selection is not null)
        {
            return ResolveTextSelectionAsync(selector.Selection, cancellationToken);
        }

        return ValueTask.FromResult(SelectorResolveResult<ResolvedDocumentSpan>.NotFound());
    }

    private async ValueTask<SelectorResolveResult<ResolvedDocumentSpan>> ResolveTextSelectionAsync(TextSelectionSelector selector, CancellationToken cancellationToken)
    {
        if (selector.Document is null || string.IsNullOrEmpty(selector.SelectedText))
        {
            return SelectorResolveResult<ResolvedDocumentSpan>.NotFound();
        }

        var documentResolution = ResolveDocument(selector.Document);
        if (!documentResolution.IsResolved)
        {
            return documentResolution.Status == SelectorResolveStatus.Ambiguous
                ? SelectorResolveResult<ResolvedDocumentSpan>.Ambiguous()
                : SelectorResolveResult<ResolvedDocumentSpan>.NotFound();
        }

        var document = documentResolution.Value;
        var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var text = sourceText.ToString();
        var matches = new List<int>();
        var searchStart = 0;

        while (searchStart <= text.Length - selector.SelectedText.Length)
        {
            var matchIndex = text.IndexOf(selector.SelectedText, searchStart, StringComparison.Ordinal);
            if (matchIndex < 0)
            {
                break;
            }

            if (MatchesSelectionContext(text, selector, matchIndex))
            {
                matches.Add(matchIndex);
            }

            searchStart = matchIndex + 1;
        }

        return matches.Count switch
        {
            1 => SelectorResolveResult<ResolvedDocumentSpan>.Resolved(new ResolvedDocumentSpan
            {
                Document = document,
                Span = new TextSpan(matches[0], selector.SelectedText.Length),
            }),
            > 1 => SelectorResolveResult<ResolvedDocumentSpan>.Ambiguous(),
            _ => SelectorResolveResult<ResolvedDocumentSpan>.NotFound(),
        };
    }

    private async ValueTask<SelectorResolveResult<ResolvedDocumentSpan>> ResolveTextSpanAsync(TextSpanSelector selector, CancellationToken cancellationToken)
    {
        if (selector.Document is null)
        {
            return SelectorResolveResult<ResolvedDocumentSpan>.NotFound();
        }

        var documentResolution = ResolveDocument(selector.Document);
        if (!documentResolution.IsResolved)
        {
            return documentResolution.Status == SelectorResolveStatus.Ambiguous
                ? SelectorResolveResult<ResolvedDocumentSpan>.Ambiguous()
                : SelectorResolveResult<ResolvedDocumentSpan>.NotFound();
        }

        var document = documentResolution.Value;
        var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        if (selector.Start < 0 || selector.Length < 0 || selector.Start + selector.Length > sourceText.Length)
        {
            return SelectorResolveResult<ResolvedDocumentSpan>.NotFound();
        }

        return SelectorResolveResult<ResolvedDocumentSpan>.Resolved(new ResolvedDocumentSpan
        {
            Document = document,
            Span = new TextSpan(selector.Start, selector.Length),
        });
    }

    private async ValueTask<SelectorResolveResult<ISymbol>> ResolveSymbolByDocumentationCommentIdAsync(string documentationCommentId, CancellationToken cancellationToken)
    {
        var matches = new List<ISymbol>();
        foreach (var project in _solution.Projects.Where(static project => project.SupportsCompilation))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            if (compilation is null)
            {
                continue;
            }

            var candidateSymbols = new[]
            {
                DocumentationCommentId.GetFirstSymbolForDeclarationId(documentationCommentId, compilation),
                DocumentationCommentId.GetFirstSymbolForReferenceId(documentationCommentId, compilation),
            };

            foreach (var symbol in candidateSymbols)
            {
                if (symbol is not null && symbol.Locations.Any(static location => location.IsInSource))
                {
                    matches.Add(symbol);
                }
            }
        }

        var distinctMatches = matches
            .Distinct(SymbolEqualityComparer.Default)
            .ToArray();

        return distinctMatches.Length switch
        {
            1 => SelectorResolveResult<ISymbol>.Resolved(distinctMatches[0]),
            > 1 => SelectorResolveResult<ISymbol>.Ambiguous(),
            _ => SelectorResolveResult<ISymbol>.NotFound(),
        };
    }

    private static bool MatchesSelectionContext(string text, TextSelectionSelector selector, int matchIndex)
    {
        if (!string.IsNullOrEmpty(selector.ContextBefore))
        {
            var start = matchIndex - selector.ContextBefore.Length;
            if (start < 0 || !string.Equals(text.Substring(start, selector.ContextBefore.Length), selector.ContextBefore, StringComparison.Ordinal))
            {
                return false;
            }
        }

        if (!string.IsNullOrEmpty(selector.ContextAfter))
        {
            var afterIndex = matchIndex + selector.SelectedText.Length;
            if (afterIndex + selector.ContextAfter.Length > text.Length
                || !string.Equals(text.Substring(afterIndex, selector.ContextAfter.Length), selector.ContextAfter, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var fullPath = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(_workspaceRoot, path));

        if (string.IsNullOrWhiteSpace(_workspaceRoot))
        {
            return fullPath.Replace('\\', '/');
        }

        return Path.GetRelativePath(_workspaceRoot, fullPath).Replace('\\', '/');
    }
}
