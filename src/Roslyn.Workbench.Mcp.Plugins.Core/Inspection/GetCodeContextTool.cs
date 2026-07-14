using Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("get-code-context", "Get Code Context", "Returns a bounded code window with the enclosing semantic context for a selected location.")]
internal sealed class GetCodeContextTool : QueryToolHandler<GetCodeContextRequest, CodeContextData>
{
    protected override async ValueTask<PluginExecutionResult<CodeContextData>> ExecuteCoreAsync(GetCodeContextRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var locationResolution = await ResolveLocationAsync(request.Location, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
        if (locationResolution.Rejection is not null)
        {
            return locationResolution.Rejection;
        }

        if (locationResolution.SemanticModel is null
            || locationResolution.Node is null
            || locationResolution.Location is null
            || locationResolution.Document is null)
        {
            throw new InvalidOperationException("A successful location resolution must contain a document, location, node and semantic model.");
        }

        var enclosingSymbols = request.IncludeEnclosingSymbols
            ? GetEnclosingSymbols(locationResolution.SemanticModel, locationResolution.Node, context)
            : [];
        var diagnostics = request.IncludeDiagnostics
            ? locationResolution.SemanticModel.GetDiagnostics(locationResolution.Location.SourceSpan)
                .Distinct(DiagnosticLocationComparer.Instance)
                .Select(diagnostic => new DiagnosticInfo
                {
                    Id = diagnostic.Id,
                    Severity = InspectionProjectionFactory.MapSeverity(diagnostic.Severity),
                    Message = diagnostic.GetMessage(),
                    Location = diagnostic.Location.IsInSource ? context.WorkspaceResolver.CreateResolvedLocation(diagnostic.Location) : null,
                })
                .OrderBy(static diagnostic => diagnostic.Location?.Document?.Path ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(static diagnostic => diagnostic.Location?.Span?.Start ?? int.MaxValue)
                .ThenBy(static diagnostic => diagnostic.Id, StringComparer.Ordinal)
                .ToArray()
            : [];

        var text = await locationResolution.Document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var lines = text.Lines;
        var startLine = lines.GetLineFromPosition(locationResolution.Location.SourceSpan.Start).LineNumber;
        var endPosition = Math.Max(locationResolution.Location.SourceSpan.Start, locationResolution.Location.SourceSpan.End - 1);
        var endLine = lines.GetLineFromPosition(endPosition).LineNumber;
        var windowStart = Math.Max(0, startLine - Math.Max(0, request.BeforeLines));
        var windowEnd = Math.Min(lines.Count - 1, endLine + Math.Max(0, request.AfterLines));
        var windowText = string.Join(
            Environment.NewLine,
            Enumerable.Range(windowStart, windowEnd - windowStart + 1).Select(index => lines[index].ToString()));

        return PluginExecutionResult<CodeContextData>.Success(new CodeContextData
        {
            Location = locationResolution.ResolvedLocation,
            Text = windowText,
            EnclosingSymbols = enclosingSymbols,
            Diagnostics = diagnostics,
        });
    }

    private static IReadOnlyList<SymbolReference> GetEnclosingSymbols(SemanticModel semanticModel, SyntaxNode node, IQueryContext context)
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

    private static async ValueTask<LocationResolution> ResolveLocationAsync(LocationSelector? selector, SnapshotPrecondition? expectedSnapshot, IQueryContext context, CancellationToken cancellationToken)
    {
        var snapshotRejection = context.ToolExecutionServices.RequestResolver.ValidateSnapshot<CodeContextData>(context, expectedSnapshot);
        if (snapshotRejection is not null)
        {
            return new LocationResolution
            {
                Rejection = snapshotRejection,
            };
        }

        if (selector is null)
        {
            return new LocationResolution
            {
                Rejection = ToolExecutionHelpers.Rejected<CodeContextData>("InvalidRequest", "A location selector is required."),
            };
        }

        var location = await context.WorkspaceResolver.ResolveLocationAsync(selector, cancellationToken).ConfigureAwait(false);
        if (!location.IsResolved)
        {
            return new LocationResolution
            {
                Rejection = ToolExecutionHelpers.RejectFromStatus<CodeContextData>(location.Status, "Location"),
            };
        }

        var sourceLocation = location.Value;
        var document = sourceLocation.SourceTree is null
            ? null
            : context.CurrentSolution.GetDocument(sourceLocation.SourceTree);
        var resolvedLocation = context.WorkspaceResolver.CreateResolvedLocation(sourceLocation);
        if (document is null || resolvedLocation?.Document?.Path is null)
        {
            return new LocationResolution
            {
                Rejection = ToolExecutionHelpers.Rejected<CodeContextData>("LocationNotFound", "The location selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain),
            };
        }

        var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (syntaxRoot is null || semanticModel is null)
        {
            return new LocationResolution
            {
                Rejection = ToolExecutionHelpers.Rejected<CodeContextData>("LocationNotFound", "The location selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain),
            };
        }

        return new LocationResolution
        {
            Document = document,
            Location = sourceLocation,
            Node = syntaxRoot.FindNode(sourceLocation.SourceSpan, getInnermostNodeForTie: true),
            ResolvedLocation = resolvedLocation,
            SemanticModel = semanticModel,
        };
    }

    private sealed record LocationResolution
    {
        public PluginExecutionResult<CodeContextData>? Rejection { get; init; }

        public Document? Document { get; init; }

        public Location? Location { get; init; }

        public SyntaxNode? Node { get; init; }

        public ResolvedLocation? ResolvedLocation { get; init; }

        public SemanticModel? SemanticModel { get; init; }
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
                && string.Equals(x.GetMessage(), y.GetMessage(), StringComparison.Ordinal)
                && x.Severity == y.Severity
                && x.Location.SourceSpan.Equals(y.Location.SourceSpan)
                && string.Equals(x.Location.SourceTree?.FilePath, y.Location.SourceTree?.FilePath, StringComparison.Ordinal);
        }

        public int GetHashCode(Diagnostic obj)
        {
            return HashCode.Combine(
                obj.Id,
                obj.GetMessage(),
                obj.Severity,
                obj.Location.SourceSpan,
                obj.Location.SourceTree?.FilePath);
        }
    }
}
