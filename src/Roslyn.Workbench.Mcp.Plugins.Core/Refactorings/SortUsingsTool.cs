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
        if (await document.GetSyntaxRootAsync(cancellationToken) is not CompilationUnitSyntax root)
        {
            return PluginExecutionResult.Rejected<MutationCandidate>("InvalidRequest", "Sort usings requires a compilation unit root.");
        }

        var orderedUsings = root.Usings
            .OrderBy(item => request.SystemFirst ? !IsSystemUsing(item) : false)
            .ThenBy(static item => GetNamespaceName(item) ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static item => item.Alias?.Name.Identifier.ValueText ?? string.Empty, StringComparer.Ordinal)
            .ToArray();

        if (root.Usings.SequenceEqual(orderedUsings))
        {
            return PluginExecutionResult.NoChange<MutationCandidate>();
        }

        var updatedRoot = root.WithUsings(SyntaxFactory.List(orderedUsings));
        var updatedDocument = document.WithSyntaxRoot(updatedRoot);

        var candidate = new MutationCandidate
        {
            CandidateSolution = updatedDocument.Project.Solution,
            Summary = $"Sort using directives in '{document.Name}'.",
        };

        return PluginExecutionResult.Success(candidate);
    }

    private static bool IsSystemUsing(UsingDirectiveSyntax usingDirective)
    {
        var namespaceName = GetNamespaceName(usingDirective);
        if (namespaceName is null)
        {
            return false;
        }

        return string.Equals(namespaceName, "System", StringComparison.Ordinal)
            || namespaceName.StartsWith("System.", StringComparison.Ordinal);
    }

    private static string? GetNamespaceName(UsingDirectiveSyntax usingDirective)
    {
        var namespaceName = usingDirective.Name?.ToString();
        if (namespaceName is null)
        {
            return null;
        }

        const string globalAlias = "global::";
        if (namespaceName.StartsWith(globalAlias, StringComparison.Ordinal))
        {
            namespaceName = namespaceName[globalAlias.Length..];
        }

        return namespaceName;
    }
}
