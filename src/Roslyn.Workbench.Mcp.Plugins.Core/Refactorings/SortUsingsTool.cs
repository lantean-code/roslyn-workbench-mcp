using Roslyn.Workbench.Mcp.Contracts.Inspection;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Refactorings;

internal sealed class SortUsingsTool : MutationToolHandler<SortUsingsRequest>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "sort-usings",
        Title = "Sort Usings",
        Description = "Stages an ordered set of using directives for one document.",
        Behavior = new ToolBehaviorHints
        {
            Destructive = true,
        },
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new SortUsingsTool());
    }

    protected override async ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(SortUsingsRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        var documentResolution = context.ToolExecutionServices.RequestResolver.ResolveDocument<MutationProposal>(request.Document, context);
        if (documentResolution.HasRejection)
        {
            return documentResolution.Rejection;
        }

        var snapshotRejection = context.ToolExecutionServices.RequestResolver.ValidateSnapshot<MutationProposal>(context, request.ExpectedSnapshot);
        if (snapshotRejection is not null)
        {
            return snapshotRejection;
        }

        var document = documentResolution.Value;
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false) as CompilationUnitSyntax;
        if (root is null)
        {
            return ToolExecutionHelpers.Rejected<MutationProposal>("InvalidRequest", "Sort usings requires a compilation unit root.");
        }

        var orderedUsings = root.Usings
            .OrderBy(item => request.SystemFirst ? !IsSystemUsing(item) : false)
            .ThenBy(static item => item.Name?.ToString() ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static item => item.Alias?.Name.Identifier.ValueText ?? string.Empty, StringComparer.Ordinal)
            .ToArray();
        if (root.Usings.SequenceEqual(orderedUsings))
        {
            return PluginExecutionResult<MutationProposal>.NoChange();
        }

        var updatedRoot = root.WithUsings(SyntaxFactory.List(orderedUsings));
        var updatedDocument = document.WithSyntaxRoot(updatedRoot);

        return PluginExecutionResult<MutationProposal>.Success(new MutationProposal
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
