using Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("find-duplicate-code", "Find Duplicate Code", "Returns duplicate executable blocks that normalize to the same statement sequence.")]
internal sealed class FindDuplicateCodeTool : QueryToolHandler<FindDuplicateCodeRequest, DuplicateCodeData>
{
    protected override async ValueTask<PluginExecutionResult<DuplicateCodeData>> ExecuteCoreAsync(FindDuplicateCodeRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        if (request.MinimumStatements < 1)
        {
            return ToolExecutionHelpers.Rejected<DuplicateCodeData>("InvalidRequest", "MinimumStatements must be at least 1.");
        }

        var documents = context.ToolExecutionServices.RequestResolver.ResolveDocuments<DuplicateCodeData>(request.Scope, context);
        if (documents.HasRejection)
        {
            return documents.Rejection;
        }

        var groups = await FindDuplicateGroupsAsync(
            documents.Value,
            context,
            request.MinimumStatements,
            cancellationToken).ConfigureAwait(false);

        return PluginExecutionResult<DuplicateCodeData>.Success(new DuplicateCodeData
        {
            Groups = ToolExecutionHelpers.CreateBoundedCollection(
                groups,
                ToolExecutionHelpers.GetMaxResults(context, request.GroupsLimit)),
        });
    }

    private static async ValueTask<IReadOnlyList<DuplicateCodeGroup>> FindDuplicateGroupsAsync(
        IReadOnlyList<Document> documents,
        IQueryContext context,
        int minimumStatements,
        CancellationToken cancellationToken)
    {
        var candidates = new List<DuplicateCandidate>();
        foreach (var document in documents.OrderBy(static item => item.FilePath, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (syntaxRoot is null || semanticModel is null)
            {
                continue;
            }

            foreach (var executableBlock in GetExecutableBlocks(syntaxRoot))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var statements = executableBlock.Statements;
                if (statements.Count < minimumStatements)
                {
                    continue;
                }

                var normalizedKey = string.Join(
                    "\n",
                    statements.Select(static statement => NormalizeStatement(statement)));
                if (string.IsNullOrWhiteSpace(normalizedKey))
                {
                    continue;
                }

                var symbol = semanticModel.GetEnclosingSymbol(executableBlock.SpanStart, cancellationToken);
                if (symbol is null)
                {
                    continue;
                }

                var resolvedLocation = context.WorkspaceResolver.CreateResolvedLocation(executableBlock.GetLocation());
                if (resolvedLocation is null)
                {
                    continue;
                }

                candidates.Add(new DuplicateCandidate
                {
                    Key = normalizedKey,
                    StatementCount = statements.Count,
                    Occurrence = new DuplicateCodeOccurrence
                    {
                        Symbol = context.WorkspaceResolver.CreateSymbolReference(symbol),
                        Location = resolvedLocation,
                        Context = CreateContext(statements),
                    },
                });
            }
        }

        return candidates
            .GroupBy(static candidate => candidate.Key, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(group => new DuplicateCodeGroup
            {
                StatementCount = group.First().StatementCount,
                Occurrences = group
                    .Select(static candidate => candidate.Occurrence)
                    .OrderBy(static occurrence => occurrence.Location?.Document?.Path ?? string.Empty, StringComparer.Ordinal)
                    .ThenBy(static occurrence => occurrence.Location?.Span?.Start ?? int.MaxValue)
                    .ToArray(),
            })
            .OrderByDescending(static group => group.StatementCount)
            .ThenBy(static group => group.Occurrences[0].Symbol?.DisplayName ?? string.Empty, StringComparer.Ordinal)
            .ToArray();
    }

    private static string CreateContext(IReadOnlyList<StatementSyntax> statements)
    {
        return string.Join(" ", statements.Select(static statement => statement.ToString().ReplaceLineEndings(" ").Trim()));
    }

    private static IEnumerable<BlockSyntax> GetExecutableBlocks(SyntaxNode syntaxRoot)
    {
        return syntaxRoot.DescendantNodes().Select(GetExecutableBlock).OfType<BlockSyntax>();
    }

    private static BlockSyntax? GetExecutableBlock(SyntaxNode node)
    {
        return node switch
        {
            MethodDeclarationSyntax { Body: not null } methodDeclaration => methodDeclaration.Body,
            ConstructorDeclarationSyntax { Body: not null } constructorDeclaration => constructorDeclaration.Body,
            LocalFunctionStatementSyntax { Body: not null } localFunction => localFunction.Body,
            AccessorDeclarationSyntax { Body: not null } accessor => accessor.Body,
            _ => null,
        };
    }

    private static string NormalizeStatement(StatementSyntax statement)
    {
        return statement.NormalizeWhitespace(elasticTrivia: false).ToFullString().Trim();
    }

    private sealed record DuplicateCandidate
    {
        public string Key { get; init; } = string.Empty;

        public int StatementCount { get; init; }

        public DuplicateCodeOccurrence Occurrence { get; init; } = new();
    }
}
