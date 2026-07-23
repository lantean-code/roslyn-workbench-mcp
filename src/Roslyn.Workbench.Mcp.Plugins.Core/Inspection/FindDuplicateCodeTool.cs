namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("find-duplicate-code", "Find Duplicate Code", "Returns duplicate executable blocks that normalize to the same statement sequence.")]
internal sealed class FindDuplicateCodeTool : QueryToolHandler<FindDuplicateCodeRequest, DuplicateCodeData>
{
    protected override async ValueTask<PluginExecutionResult<DuplicateCodeData>> ExecuteCoreAsync(FindDuplicateCodeRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        if (request.MinimumStatements < 1)
        {
            return PluginExecutionResultFactory.Rejected<DuplicateCodeData>("InvalidRequest", "MinimumStatements must be at least 1.");
        }

        var documents = context.ToolExecutionServices.RequestResolver.ResolveDocuments<DuplicateCodeData>(request.Scope, context);
        if (documents.HasRejection)
        {
            return documents.Rejection;
        }

        var (groups, totalCount) = await FindDuplicateGroupsAsync(
            documents.Value,
            context,
            request.MinimumStatements,
            request.EffectiveGroupsLimit,
            cancellationToken);

        var data = new DuplicateCodeData
        {
            Groups = BoundedCollection<DuplicateCodeGroup>.CreatePrebounded(groups, totalCount),
        };

        return PluginExecutionResult<DuplicateCodeData>.Success(data);
    }

    private static async ValueTask<(IReadOnlyList<DuplicateCodeGroup> Groups, int TotalCount)> FindDuplicateGroupsAsync(
        IReadOnlyList<Document> documents,
        IQueryContext context,
        int minimumStatements,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var candidates = new List<DuplicateCandidate>();
        foreach (var document in documents.OrderBy(static item => item.FilePath, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
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
                    Statements = statements,
                    Symbol = symbol,
                    Location = resolvedLocation,
                });
            }
        }

        var candidatesByKey = new Dictionary<string, List<DuplicateCandidate>>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            if (!candidatesByKey.TryGetValue(candidate.Key, out var matchingCandidates))
            {
                matchingCandidates = [];
                candidatesByKey.Add(candidate.Key, matchingCandidates);
            }

            matchingCandidates.Add(candidate);
        }

        var groupCandidates = new List<DuplicateGroupCandidate>();
        var discoveryOrder = 0;
        foreach (var matchingCandidates in candidatesByKey.Values)
        {
            if (matchingCandidates.Count < 2)
            {
                discoveryOrder++;
                continue;
            }

            matchingCandidates.Sort(CompareDuplicateCandidates);
            groupCandidates.Add(new DuplicateGroupCandidate
            {
                StatementCount = matchingCandidates[0].StatementCount,
                FirstSymbolDisplayName = matchingCandidates[0].Symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                DiscoveryOrder = discoveryOrder,
                Occurrences = matchingCandidates,
            });

            discoveryOrder++;
        }

        groupCandidates.Sort(CompareDuplicateGroups);
        var selectedGroupCount = Math.Min(groupCandidates.Count, maxResults);
        var groups = new List<DuplicateCodeGroup>();
        for (var index = 0; index < selectedGroupCount; index++)
        {
            var groupCandidate = groupCandidates[index];
            var occurrences = new List<DuplicateCodeOccurrence>();
            foreach (var candidate in groupCandidate.Occurrences)
            {
                occurrences.Add(new DuplicateCodeOccurrence
                {
                    Symbol = context.WorkspaceResolver.CreateSymbolReference(candidate.Symbol),
                    Location = candidate.Location,
                    Context = CreateContext(candidate.Statements),
                });
            }

            groups.Add(new DuplicateCodeGroup
            {
                StatementCount = groupCandidate.StatementCount,
                Occurrences = occurrences,
            });
        }

        return (groups, groupCandidates.Count);
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

    private static int CompareDuplicateCandidates(DuplicateCandidate left, DuplicateCandidate right)
    {
        var pathComparison = StringComparer.Ordinal.Compare(left.Location.Document?.Path ?? string.Empty, right.Location.Document?.Path ?? string.Empty);
        if (pathComparison != 0)
        {
            return pathComparison;
        }

        return (left.Location.Span?.Start ?? int.MaxValue).CompareTo(right.Location.Span?.Start ?? int.MaxValue);
    }

    private static int CompareDuplicateGroups(DuplicateGroupCandidate left, DuplicateGroupCandidate right)
    {
        var statementCountComparison = right.StatementCount.CompareTo(left.StatementCount);
        if (statementCountComparison != 0)
        {
            return statementCountComparison;
        }

        var displayNameComparison = StringComparer.Ordinal.Compare(left.FirstSymbolDisplayName, right.FirstSymbolDisplayName);
        return displayNameComparison != 0
            ? displayNameComparison
            : left.DiscoveryOrder.CompareTo(right.DiscoveryOrder);
    }

    private sealed record DuplicateCandidate
    {
        public required string Key { get; init; }

        public int StatementCount { get; init; }

        public required IReadOnlyList<StatementSyntax> Statements { get; init; }

        public required ISymbol Symbol { get; init; }

        public required ResolvedLocation Location { get; init; }
    }

    private sealed record DuplicateGroupCandidate
    {
        public int StatementCount { get; init; }

        public required string FirstSymbolDisplayName { get; init; }

        public int DiscoveryOrder { get; init; }

        public required IReadOnlyList<DuplicateCandidate> Occurrences { get; init; }
    }
}
