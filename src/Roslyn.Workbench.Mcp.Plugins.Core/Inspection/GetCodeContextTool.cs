namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("get-code-context", "Get Code Context", "Returns a bounded code window with the enclosing semantic context for a selected location.")]
internal sealed class GetCodeContextTool : QueryToolHandler<GetCodeContextRequest, CodeContextData>
{
    protected override async ValueTask<PluginExecutionResult<CodeContextData>> ExecuteCoreAsync(GetCodeContextRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var locationResolution = await ResolveLocationAsync(request.Location, request.ExpectedSnapshot, context, cancellationToken);
        if (locationResolution.HasRejection)
        {
            return locationResolution.Rejection;
        }

        var resolvedLocation = locationResolution.Value;

        var enclosingSymbols = request.IncludeEnclosingSymbols
            ? GetEnclosingSymbols(resolvedLocation.SemanticModel, resolvedLocation.Node, context)
            : [];

        var diagnostics = request.IncludeDiagnostics
            ? CreateDiagnostics(resolvedLocation, context, cancellationToken)
            : [];

        var text = await resolvedLocation.Document.GetTextAsync(cancellationToken);
        var lines = text.Lines;
        var startLine = lines.GetLineFromPosition(resolvedLocation.Location.SourceSpan.Start).LineNumber;
        var endPosition = Math.Max(resolvedLocation.Location.SourceSpan.Start, resolvedLocation.Location.SourceSpan.End - 1);
        var endLine = lines.GetLineFromPosition(endPosition).LineNumber;
        var windowStart = Math.Max(0, startLine - Math.Max(0, request.BeforeLines));
        var windowEnd = Math.Min(lines.Count - 1, endLine + Math.Max(0, request.AfterLines));
        var windowText = string.Join(
            Environment.NewLine,
            Enumerable.Range(windowStart, windowEnd - windowStart + 1).Select(index => lines[index].ToString()));

        var data = new CodeContextData
        {
            Location = resolvedLocation.ResolvedLocation,
            Text = windowText,
            EnclosingSymbols = enclosingSymbols,
            Diagnostics = diagnostics,
        };

        return PluginExecutionResult<CodeContextData>.Success(data);
    }

    private static List<SymbolReference> GetEnclosingSymbols(SemanticModel semanticModel, SyntaxNode node, IQueryContext context)
    {
        var symbols = new List<SymbolReference>();
        for (var current = semanticModel.GetEnclosingSymbol(node.SpanStart); current is not null; current = current.ContainingSymbol)
        {
            if (current is INamespaceSymbol { IsGlobalNamespace: true })
            {
                continue;
            }

            symbols.Add(context.WorkspaceResolver.CreateSymbolReference(current));
        }

        return symbols;
    }

    private static DiagnosticInfo[] CreateDiagnostics(ResolvedCodeContext resolvedLocation, IQueryContext context, CancellationToken cancellationToken)
    {
        var sourceDiagnostics = resolvedLocation.SemanticModel.GetDiagnostics(resolvedLocation.Location.SourceSpan, cancellationToken);
        var uniqueDiagnostics = new HashSet<Diagnostic>(DiagnosticLocationComparer.Instance);
        var diagnostics = new List<DiagnosticInfo>();
        foreach (var diagnostic in sourceDiagnostics)
        {
            if (!uniqueDiagnostics.Add(diagnostic))
            {
                continue;
            }

            diagnostics.Add(new DiagnosticInfo
            {
                Id = diagnostic.Id,
                Severity = InspectionProjectionFactory.MapSeverity(diagnostic.Severity),
                Message = diagnostic.GetMessage(CultureInfo.InvariantCulture),
                Location = diagnostic.Location.IsInSource ? context.WorkspaceResolver.CreateResolvedLocation(diagnostic.Location) : null,
            });
        }

        return diagnostics
            .OrderBy(static diagnostic => diagnostic.Location?.Document?.Path ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.Location?.Span?.Start ?? int.MaxValue)
            .ThenBy(static diagnostic => diagnostic.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static async ValueTask<ToolResolutionResult<ResolvedCodeContext, CodeContextData>> ResolveLocationAsync(LocationSelector? selector, SnapshotPrecondition? expectedSnapshot, IQueryContext context, CancellationToken cancellationToken)
    {
        var snapshotRejection = context.ToolExecutionServices.RequestResolver.ValidateSnapshot<CodeContextData>(context, expectedSnapshot);
        if (snapshotRejection is not null)
        {
            return ToolResolutionResult<ResolvedCodeContext, CodeContextData>.Rejected(snapshotRejection);
        }

        if (selector is null)
        {
            return ToolResolutionResult<ResolvedCodeContext, CodeContextData>.Rejected(ToolExecutionHelpers.Rejected<CodeContextData>("InvalidRequest", "A location selector is required."));
        }

        var location = await context.WorkspaceResolver.ResolveLocationAsync(selector, cancellationToken);
        if (!location.IsResolved)
        {
            return ToolResolutionResult<ResolvedCodeContext, CodeContextData>.Rejected(ToolExecutionHelpers.RejectFromStatus<CodeContextData>(location.Status, "Location", "location"));
        }

        var sourceLocation = location.Value;
        var document = sourceLocation.SourceTree is null
            ? null
            : context.CurrentSolution.GetDocument(sourceLocation.SourceTree);

        var resolvedLocation = context.WorkspaceResolver.CreateResolvedLocation(sourceLocation);
        if (document is null || resolvedLocation?.Document?.Path is null)
        {
            return ToolResolutionResult<ResolvedCodeContext, CodeContextData>.Rejected(ToolExecutionHelpers.Rejected<CodeContextData>("LocationNotFound", "The location selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain));
        }

        var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (syntaxRoot is null || semanticModel is null)
        {
            return ToolResolutionResult<ResolvedCodeContext, CodeContextData>.Rejected(ToolExecutionHelpers.Rejected<CodeContextData>("LocationNotFound", "The location selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain));
        }

        return ToolResolutionResult<ResolvedCodeContext, CodeContextData>.Resolved(new ResolvedCodeContext(
                document,
                sourceLocation,
                syntaxRoot.FindNode(sourceLocation.SourceSpan, getInnermostNodeForTie: true),
                resolvedLocation,
                semanticModel));
    }

    private sealed record ResolvedCodeContext
    {
        public Document Document { get; }

        public Location Location { get; }

        public SyntaxNode Node { get; }

        public ResolvedLocation ResolvedLocation { get; }

        public SemanticModel SemanticModel { get; }

        public ResolvedCodeContext(
            Document document,
            Location location,
            SyntaxNode node,
            ResolvedLocation resolvedLocation,
            SemanticModel semanticModel)
        {
            Document = document;
            Location = location;
            Node = node;
            ResolvedLocation = resolvedLocation;
            SemanticModel = semanticModel;
        }
    }

    private sealed class DiagnosticLocationComparer : IEqualityComparer<Diagnostic>
    {
        public static readonly DiagnosticLocationComparer Instance = new();

        public bool Equals(Diagnostic? x, Diagnostic? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return string.Equals(x.Id, y.Id, StringComparison.Ordinal)
                && string.Equals(x.GetMessage(CultureInfo.InvariantCulture), y.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                && x.Severity == y.Severity
                && x.Location.SourceSpan.Equals(y.Location.SourceSpan)
                && string.Equals(x.Location.SourceTree?.FilePath, y.Location.SourceTree?.FilePath, StringComparison.Ordinal);
        }

        public int GetHashCode(Diagnostic obj)
        {
            return HashCode.Combine(
                obj.Id,
                obj.GetMessage(CultureInfo.InvariantCulture),
                obj.Severity,
                obj.Location.SourceSpan,
                obj.Location.SourceTree?.FilePath);
        }
    }
}
