namespace Roslyn.Workbench.Mcp.Workspace.Resolution;

internal sealed class WorkspaceResolver : IWorkspaceResolver
{
    private readonly Solution _solution;
    private readonly SnapshotPrecondition? _snapshot;
    private readonly WorkspaceProjectTargetFrameworkMap _projectTargetFrameworks;
    private readonly IWorkspacePathComparison _workspacePathComparison;
    private readonly IWorkspacePathService _workspacePathService;

    public WorkspaceResolver(
        Solution solution,
        SnapshotPrecondition? snapshot,
        WorkspaceProjectTargetFrameworkMap projectTargetFrameworks,
        IWorkspacePathComparison workspacePathComparison,
        IWorkspacePathService workspacePathService)
    {
        _solution = solution;
        _snapshot = snapshot;
        _projectTargetFrameworks = projectTargetFrameworks;
        _workspacePathComparison = workspacePathComparison;
        _workspacePathService = workspacePathService;
    }

    public ResolvedLocation? CreateResolvedLocation(Location location)
    {
        if (!location.IsInSource || location.SourceTree is null || _snapshot is null)
        {
            return null;
        }

        var text = location.SourceTree.GetText();
        var span = location.SourceSpan;
        var linePosition = text.Lines.GetLinePosition(span.Start);
        var document = _solution.GetDocument(location.SourceTree);

        return new ResolvedLocation
        {
            Document = document is null ? null : CreateDocumentReference(document),
            Span = new TextSpanRange
            {
                Start = span.Start,
                Length = span.Length,
            },
            Line = linePosition.Line + 1,
            Column = linePosition.Character + 1,
            Snapshot = _snapshot,
        };
    }

    public DocumentReference? CreateDocumentReference(Document document)
    {
        if (!_workspacePathService.TryNormalizePath(document.FilePath ?? string.Empty, out var normalizedPath))
        {
            return null;
        }

        return new DocumentReference
        {
            DocumentId = document.Id.Id.ToString(),
            ProjectId = document.Project.Id.Id.ToString(),
            Path = normalizedPath,
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

        if (_snapshot is null)
        {
            return SnapshotMatchResult.WorkspaceEpochMismatch();
        }

        if (precondition.WorkspaceId != _snapshot.WorkspaceId)
        {
            return SnapshotMatchResult.WorkspaceEpochMismatch();
        }

        if (precondition.WorkspaceEpoch != _snapshot.WorkspaceEpoch)
        {
            return SnapshotMatchResult.WorkspaceEpochMismatch();
        }

        if (precondition.SnapshotId != _snapshot.SnapshotId)
        {
            return SnapshotMatchResult.SnapshotIdMismatch();
        }

        if (precondition.TransactionRevision != _snapshot.TransactionRevision)
        {
            return SnapshotMatchResult.TransactionRevisionMismatch();
        }

        return SnapshotMatchResult.Matched();
    }

    public SelectorResolveResult<Document> ResolveDocument(DocumentSelector selector)
    {
        return ResolveDocument(selector, project: null);
    }

    public async ValueTask<SelectorResolveResult<Location>> ResolveLocationAsync(LocationSelector selector, CancellationToken cancellationToken)
    {
        var resolution = await ResolveDocumentSpanAsync(selector, project: null, cancellationToken);
        if (resolution.IsResolved)
        {
            var syntaxTree = await resolution.Value.Document.GetSyntaxTreeAsync(cancellationToken);
            if (syntaxTree is null)
            {
                return SelectorResolveResult.NotFound<Location>();
            }

            var location = syntaxTree.GetLocation(resolution.Value.Span);

            return SelectorResolveResult.Resolved(location);
        }

        return CreateUnresolvedResult<Location>(resolution.Status);
    }

    public SelectorResolveResult<Project> ResolveProject(ProjectSelector selector)
    {
        string? normalizedSelectorPath = null;
        if (!string.IsNullOrWhiteSpace(selector.Path))
        {
            if (!_workspacePathService.TryNormalizePath(selector.Path, out normalizedSelectorPath))
            {
                return SelectorResolveResult.Invalid<Project>();
            }
        }

        var matches = _solution.Projects
            .Where(project => MatchesProjectSelector(project, selector, normalizedSelectorPath))
            .ToArray();
        return matches.Length switch
        {
            1 => SelectorResolveResult.Resolved(matches[0]),
            > 1 => SelectorResolveResult.Ambiguous<Project>(),
            _ => SelectorResolveResult.NotFound<Project>(),
        };
    }

    public async ValueTask<SelectorResolveResult<ISymbol>> ResolveSymbolAsync(SymbolSelector selector, CancellationToken cancellationToken)
    {
        Project? project = null;
        if (selector.Project is not null)
        {
            var projectResolution = ResolveProject(selector.Project);
            if (!projectResolution.IsResolved)
            {
                return CreateUnresolvedResult<ISymbol>(projectResolution.Status);
            }

            project = projectResolution.Value;
        }

        if (!string.IsNullOrWhiteSpace(selector.DocumentationCommentId))
        {
            return await ResolveSymbolByDocumentationCommentIdAsync(selector.DocumentationCommentId, project, cancellationToken);
        }

        if (selector.Location is null)
        {
            return SelectorResolveResult.NotFound<ISymbol>();
        }

        var locationResolution = await ResolveDocumentSpanAsync(selector.Location, project, cancellationToken);
        if (!locationResolution.IsResolved)
        {
            return CreateUnresolvedResult<ISymbol>(locationResolution.Status);
        }

        var document = locationResolution.Value.Document;
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (semanticModel is null)
        {
            return SelectorResolveResult.NotFound<ISymbol>();
        }

        var symbol = await SymbolFinder.FindSymbolAtPositionAsync(semanticModel, locationResolution.Value.Span.Start, _solution.Workspace, cancellationToken);
        if (symbol is not null && IsSymbolInProjectScope(symbol, project, semanticModel.Compilation))
        {
            return SelectorResolveResult.Resolved(symbol);
        }

        var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
        var node = syntaxRoot?.FindNode(locationResolution.Value.Span, getInnermostNodeForTie: true);
        if (node is not null)
        {
            symbol = semanticModel.GetDeclaredSymbol(node, cancellationToken);
            symbol ??= semanticModel.GetSymbolInfo(node, cancellationToken).Symbol;
        }

        if (symbol is null)
        {
            return SelectorResolveResult.NotFound<ISymbol>();
        }

        return SelectorResolveResult.Resolved(symbol);
    }

    private SelectorResolveResult<Document> ResolveDocument(DocumentSelector selector, Project? project)
    {
        if (selector.Project is not null)
        {
            var projectResolution = ResolveProject(selector.Project);
            if (!projectResolution.IsResolved)
            {
                return CreateUnresolvedResult<Document>(projectResolution.Status);
            }

            if (project is not null && project.Id != projectResolution.Value.Id)
            {
                return SelectorResolveResult.NotFound<Document>();
            }

            project = projectResolution.Value;
        }

        IEnumerable<Project> projects = project is null
            ? _solution.Projects
            : [project];

        if (!string.IsNullOrWhiteSpace(selector.DocumentId)
            && Guid.TryParse(selector.DocumentId, out var documentGuid))
        {
            var matchesById = new List<Document>();
            foreach (var candidateProject in projects)
            {
                foreach (var document in candidateProject.Documents)
                {
                    if (document.Id.Id == documentGuid)
                    {
                        matchesById.Add(document);
                    }
                }
            }

            if (matchesById.Count == 1)
            {
                return SelectorResolveResult.Resolved(matchesById[0]);
            }

            if (matchesById.Count > 1)
            {
                return SelectorResolveResult.Ambiguous<Document>();
            }
        }

        if (string.IsNullOrWhiteSpace(selector.Path))
        {
            return SelectorResolveResult.NotFound<Document>();
        }

        if (!_workspacePathService.TryNormalizePath(selector.Path, out var normalizedPath))
        {
            return SelectorResolveResult.Invalid<Document>();
        }

        var matches = new List<Document>();
        foreach (var candidateProject in projects)
        {
            foreach (var document in candidateProject.Documents)
            {
                var physicalDocumentPath = document.FilePath;
                if (physicalDocumentPath is not null
                    && _workspacePathService.TryNormalizePath(physicalDocumentPath, out var documentPath)
                    && PathsEqual(physicalDocumentPath, documentPath, normalizedPath))
                {
                    matches.Add(document);
                }
            }
        }

        return matches.Count switch
        {
            1 => SelectorResolveResult.Resolved(matches[0]),
            > 1 => SelectorResolveResult.Ambiguous<Document>(),
            _ => SelectorResolveResult.NotFound<Document>(),
        };
    }

    private bool MatchesProjectSelector(
        Project project,
        ProjectSelector selector,
        string? normalizedSelectorPath)
    {
        var idMatches = string.IsNullOrWhiteSpace(selector.ProjectId)
            || string.Equals(project.Id.Id.ToString(), selector.ProjectId, StringComparison.OrdinalIgnoreCase);

        var nameMatches = string.IsNullOrWhiteSpace(selector.Name)
            || string.Equals(project.Name, selector.Name, StringComparison.Ordinal);

        var pathMatches = normalizedSelectorPath is null;
        var physicalProjectPath = project.FilePath;
        if (normalizedSelectorPath is not null
            && physicalProjectPath is not null
            && _workspacePathService.TryNormalizePath(physicalProjectPath, out var normalizedProjectPath))
        {
            pathMatches = PathsEqual(physicalProjectPath, normalizedProjectPath, normalizedSelectorPath);
        }

        var targetFrameworkMatches = MatchesTargetFramework(project.Id, selector.TargetFramework);

        return idMatches && nameMatches && pathMatches && targetFrameworkMatches;
    }

    private bool PathsEqual(string physicalPath, string left, string right)
    {
        var comparison = _workspacePathComparison.GetComparison(physicalPath);
        return string.Equals(left, right, comparison);
    }

    private ValueTask<SelectorResolveResult<ResolvedDocumentSpan>> ResolveDocumentSpanAsync(
        LocationSelector selector,
        Project? project,
        CancellationToken cancellationToken)
    {
        if (selector.Span is not null)
        {
            return ResolveTextSpanAsync(selector.Span, project, cancellationToken);
        }

        if (selector.Selection is not null)
        {
            return ResolveTextSelectionAsync(selector.Selection, project, cancellationToken);
        }

        return ValueTask.FromResult(SelectorResolveResult.NotFound<ResolvedDocumentSpan>());
    }

    private async ValueTask<SelectorResolveResult<ResolvedDocumentSpan>> ResolveTextSelectionAsync(
        TextSelectionSelector selector,
        Project? project,
        CancellationToken cancellationToken)
    {
        if (selector.Document is null || string.IsNullOrEmpty(selector.SelectedText))
        {
            return SelectorResolveResult.NotFound<ResolvedDocumentSpan>();
        }

        var documentResolution = ResolveDocument(selector.Document, project);
        if (!documentResolution.IsResolved)
        {
            return CreateUnresolvedResult<ResolvedDocumentSpan>(documentResolution.Status);
        }

        var document = documentResolution.Value;
        var sourceText = await document.GetTextAsync(cancellationToken);
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

        if (matches.Count == 1)
        {
            var resolvedSpan = new ResolvedDocumentSpan
            {
                Document = document,
                Span = new TextSpan(matches[0], selector.SelectedText.Length),
            };

            return SelectorResolveResult.Resolved(resolvedSpan);
        }

        if (matches.Count > 1)
        {
            return SelectorResolveResult.Ambiguous<ResolvedDocumentSpan>();
        }

        return SelectorResolveResult.NotFound<ResolvedDocumentSpan>();
    }

    private async ValueTask<SelectorResolveResult<ResolvedDocumentSpan>> ResolveTextSpanAsync(
        TextSpanSelector selector,
        Project? project,
        CancellationToken cancellationToken)
    {
        if (selector.Document is null)
        {
            return SelectorResolveResult.NotFound<ResolvedDocumentSpan>();
        }

        var documentResolution = ResolveDocument(selector.Document, project);
        if (!documentResolution.IsResolved)
        {
            return CreateUnresolvedResult<ResolvedDocumentSpan>(documentResolution.Status);
        }

        var document = documentResolution.Value;
        var sourceText = await document.GetTextAsync(cancellationToken);
        if (selector.Start < 0
            || selector.Length < 0
            || selector.Start > sourceText.Length - selector.Length)
        {
            return SelectorResolveResult.NotFound<ResolvedDocumentSpan>();
        }

        var resolvedSpan = new ResolvedDocumentSpan
        {
            Document = document,
            Span = new TextSpan(selector.Start, selector.Length),
        };

        return SelectorResolveResult.Resolved(resolvedSpan);
    }

    private async ValueTask<SelectorResolveResult<ISymbol>> ResolveSymbolByDocumentationCommentIdAsync(
        string documentationCommentId,
        Project? project,
        CancellationToken cancellationToken)
    {
        var matches = new List<ISymbol>();
        IEnumerable<Project> projects = project is null
            ? _solution.Projects
            : [project];

        foreach (var candidateProject in projects.Where(static candidateProject => candidateProject.SupportsCompilation))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var compilation = await candidateProject.GetCompilationAsync(cancellationToken);
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
                if (symbol is not null && IsSourceSymbolInProjectScope(symbol, project, compilation))
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
            1 => SelectorResolveResult.Resolved(distinctMatches[0]),
            > 1 => SelectorResolveResult.Ambiguous<ISymbol>(),
            _ => SelectorResolveResult.NotFound<ISymbol>(),
        };
    }

    private static SelectorResolveResult<T> CreateUnresolvedResult<T>(SelectorResolveStatus status)
        where T : class
    {
        if (status == SelectorResolveStatus.Ambiguous)
        {
            return SelectorResolveResult.Ambiguous<T>();
        }

        if (status == SelectorResolveStatus.Invalid)
        {
            return SelectorResolveResult.Invalid<T>();
        }

        return SelectorResolveResult.NotFound<T>();
    }

    private bool MatchesTargetFramework(ProjectId projectId, string? targetFramework)
    {
        if (string.IsNullOrWhiteSpace(targetFramework))
        {
            return true;
        }

        return _projectTargetFrameworks.Matches(projectId, targetFramework);
    }

    private static bool IsSymbolInProjectScope(ISymbol symbol, Project? project, Compilation compilation)
    {
        if (project is null)
        {
            return true;
        }

        var hasSourceLocation = symbol.Locations.Any(static location => location.IsInSource);

        return !hasSourceLocation
            || SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly, compilation.Assembly);
    }

    private static bool IsSourceSymbolInProjectScope(ISymbol symbol, Project? project, Compilation compilation)
    {
        var hasSourceLocation = symbol.Locations.Any(static location => location.IsInSource);

        return hasSourceLocation
            && (project is null
                || SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly, compilation.Assembly));
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
}
