namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("analyze-nullability", "Analyze Nullability", "Returns nullable-flow diagnostics for a selected scope or location.")]
internal sealed class AnalyzeNullabilityTool : QueryToolHandler<AnalyzeNullabilityRequest, NullabilityAnalysisData>
{
    protected override async ValueTask<PluginExecutionResult<NullabilityAnalysisData>> ExecuteCoreAsync(AnalyzeNullabilityRequest request, IQueryContext context, CancellationToken cancellationToken)
    {

        TextSpan? selectedSpan = null;
        IReadOnlyList<Document> documents;
        if (request.Location is not null)
        {
            var snapshotRejection = context.ToolExecutionServices.RequestResolver.ValidateSnapshot<NullabilityAnalysisData>(context, request.ExpectedSnapshot);
            if (snapshotRejection is not null)
            {
                return snapshotRejection;
            }

            var locationResolution = await context.WorkspaceResolver.ResolveLocationAsync(request.Location, cancellationToken);
            if (locationResolution.Status != SelectorResolveStatus.Resolved)
            {
                return ToolExecutionHelpers.RejectFromStatus<NullabilityAnalysisData>(locationResolution.Status, "Location", "location");
            }

            if (locationResolution.Value is null || context.CurrentSolution.GetDocument(locationResolution.Value.SourceTree) is not { } document)
            {
                return ToolExecutionHelpers.Rejected<NullabilityAnalysisData>("LocationNotFound", "The location selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain);
            }

            documents = [document];
            selectedSpan = locationResolution.Value.SourceSpan;
        }
        else
        {
            var documentResolution = context.ToolExecutionServices.RequestResolver.ResolveDocuments<NullabilityAnalysisData>(request.Scope, context);
            if (documentResolution.HasRejection)
            {
                return documentResolution.Rejection;
            }

            documents = documentResolution.Value;
        }

        var diagnostics = await context.ToolExecutionServices.CompilerDiagnosticService.GetCompilerDiagnosticsAsync(documents, cancellationToken);
        var maxResults = request.EffectiveFindingsLimit;
        var findings = new List<NullabilityFinding>();
        var hasMore = false;
        var orderedDiagnostics = diagnostics
            .Where(static diagnostic => diagnostic.Id.StartsWith("CS86", StringComparison.Ordinal))
            .Where(diagnostic => selectedSpan is null || diagnostic.Location.SourceSpan.IntersectsWith(selectedSpan.Value))
            .OrderBy(static diagnostic => diagnostic.Location.SourceTree?.FilePath ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.Location.SourceSpan.Start);

        foreach (var diagnostic in orderedDiagnostics)
        {
            if (findings.Count == maxResults)
            {
                hasMore = true;
                break;
            }

            findings.Add(new NullabilityFinding
            {
                Diagnostic = CompilerDiagnosticHelpers.CreateDiagnosticInfo(diagnostic, context),
            });
        }

        return PluginExecutionResult<NullabilityAnalysisData>.Success(new NullabilityAnalysisData
        {
            Findings = ToolExecutionHelpers.CreatePreboundedCollection(
                findings,
                hasMore),
        });
    }
}
