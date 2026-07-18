namespace Roslyn.Workbench.Mcp.Plugins.Core.Refactorings;

[RoslynTool("sort-usings", "Sort Usings", "Stages an ordered set of using directives for one document.", Destructive = true)]
internal sealed class SortUsingsTool : MutationToolHandler<SortUsingsRequest>
{
    protected override async ValueTask<PluginExecutionResult<MutationCandidate>> ExecuteCoreAsync(SortUsingsRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        var documentResolution = context.ToolExecutionServices.RequestResolver.ResolveDocument<MutationCandidate>(request.Document, context);
        if (documentResolution.HasRejection)
        {
            return documentResolution.Rejection;
        }

        var snapshotRejection = context.ToolExecutionServices.RequestResolver.ValidateSnapshot<MutationCandidate>(context, request.ExpectedSnapshot);
        if (snapshotRejection is not null)
        {
            return snapshotRejection;
        }

        var document = documentResolution.Value;
        var root = await document.GetSyntaxRootAsync(cancellationToken) as CompilationUnitSyntax;
        if (root is null)
        {
            return ToolExecutionHelpers.Rejected<MutationCandidate>("InvalidRequest", "Sort usings requires a compilation unit root.");
        }

        var orderedUsings = root.Usings
            .OrderBy(item => request.SystemFirst ? !IsSystemUsing(item) : false)
            .ThenBy(static item => item.Name?.ToString() ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static item => item.Alias?.Name.Identifier.ValueText ?? string.Empty, StringComparer.Ordinal)
            .ToArray();
        if (root.Usings.SequenceEqual(orderedUsings))
        {
            return PluginExecutionResult<MutationCandidate>.NoChange();
        }

        var updatedRoot = root.WithUsings(SyntaxFactory.List(orderedUsings));
        var updatedDocument = document.WithSyntaxRoot(updatedRoot);

        return PluginExecutionResult<MutationCandidate>.Success(new MutationCandidate
        {
            CandidateSolution = updatedDocument.Project.Solution,
            Summary = $"Sort using directives in '{document.Name}'.",
        });
    }

    private static bool IsSystemUsing(UsingDirectiveSyntax usingDirective)
    {
        return usingDirective.Name?.ToString().StartsWith("System", StringComparison.Ordinal) == true;
    }
}
