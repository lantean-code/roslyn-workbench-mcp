using Roslyn.Workbench.Mcp.Contracts.Inspection;
using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal static class CodeStructureAnalysisHelpers
{
    public static async ValueTask<IReadOnlyList<DuplicateCodeGroup>> FindDuplicateGroupsAsync(
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

                var resolvedLocation = context.Resolver.CreateResolvedLocation(executableBlock.GetLocation());
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
                        Symbol = context.Resolver.CreateSymbolReference(symbol),
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

    public static async ValueTask<IReadOnlyList<MetricInfo>> GetMetricsAsync(
        GetCodeMetricsRequest request,
        IQueryContext context,
        CancellationToken cancellationToken)
    {
        var metricTargets = new List<MetricTarget>();

        if (request.Symbol is not null)
        {
            var symbolResolution = await ToolExecutionHelpers.ResolveSymbolAsync<CodeMetricsData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
            if (symbolResolution.HasRejection)
            {
                throw new MetricsResolutionException(symbolResolution.Rejection);
            }

            AddMetricTargets(symbolResolution.Value, request.IncludeChildren, metricTargets, context);
        }
        else
        {
            var documents = ToolExecutionHelpers.ResolveDocuments<CodeMetricsData>(request.Scope, context);
            if (documents.HasRejection)
            {
                throw new MetricsResolutionException(documents.Rejection);
            }

            foreach (var document in documents.Value.OrderBy(static item => item.FilePath, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
                var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
                if (syntaxRoot is null || semanticModel is null)
                {
                    continue;
                }

                foreach (var declaration in syntaxRoot.DescendantNodes().Where(IsMetricDeclarationNode))
                {
                    if (GetDeclaredSymbol(semanticModel, declaration, cancellationToken) is { } symbol && !symbol.IsImplicitlyDeclared)
                    {
                        AddMetricTargets(symbol, includeChildren: false, metricTargets, context);
                    }
                }
            }
        }

        return metricTargets
            .DistinctBy(static target => target.SymbolReference.DocumentationCommentId ?? target.SymbolReference.DisplayName, StringComparer.Ordinal)
            .OrderBy(static target => target.SymbolReference.DisplayName, StringComparer.Ordinal)
            .Select(CreateMetricInfo)
            .ToArray();
    }

    private static void AddMetricTargets(ISymbol symbol, bool includeChildren, ICollection<MetricTarget> targets, IQueryContext context)
    {
        if (TryCreateMetricTarget(symbol, context) is { } target)
        {
            targets.Add(target);
        }

        if (!includeChildren || symbol is not INamedTypeSymbol namedType)
        {
            return;
        }

        foreach (var child in namedType.GetMembers().Where(static member => !member.IsImplicitlyDeclared))
        {
            if (TryCreateMetricTarget(child, context) is { } childTarget)
            {
                targets.Add(childTarget);
            }
        }
    }

    private static MetricInfo CreateMetricInfo(MetricTarget target)
    {
        var maintainabilityIndex = Math.Clamp(100 - (target.CyclomaticComplexity * 5) - target.LogicalLines - (target.MaxNestingDepth * 3) - (target.Coupling * 2), 0, 100);

        return new MetricInfo
        {
            Symbol = target.SymbolReference,
            Location = target.Location,
            LogicalLines = target.LogicalLines,
            CyclomaticComplexity = target.CyclomaticComplexity,
            MaxNestingDepth = target.MaxNestingDepth,
            Coupling = target.Coupling,
            MaintainabilityIndex = maintainabilityIndex,
        };
    }

    private static string CreateContext(IReadOnlyList<StatementSyntax> statements)
    {
        return string.Join(" ", statements.Select(static statement => statement.ToString().ReplaceLineEndings(" ").Trim()));
    }

    private static ISymbol? GetDeclaredSymbol(SemanticModel semanticModel, SyntaxNode declaration, CancellationToken cancellationToken)
    {
        return declaration switch
        {
            BaseTypeDeclarationSyntax typeDeclaration => semanticModel.GetDeclaredSymbol(typeDeclaration, cancellationToken),
            DelegateDeclarationSyntax delegateDeclaration => semanticModel.GetDeclaredSymbol(delegateDeclaration, cancellationToken),
            BaseMethodDeclarationSyntax methodDeclaration => semanticModel.GetDeclaredSymbol(methodDeclaration, cancellationToken),
            LocalFunctionStatementSyntax localFunction => semanticModel.GetDeclaredSymbol(localFunction, cancellationToken),
            PropertyDeclarationSyntax propertyDeclaration => semanticModel.GetDeclaredSymbol(propertyDeclaration, cancellationToken),
            EventDeclarationSyntax eventDeclaration => semanticModel.GetDeclaredSymbol(eventDeclaration, cancellationToken),
            VariableDeclaratorSyntax variableDeclarator when variableDeclarator.Parent?.Parent is FieldDeclarationSyntax => semanticModel.GetDeclaredSymbol(variableDeclarator, cancellationToken),
            _ => null,
        };
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

    private static int GetMaxNestingDepth(SyntaxNode syntaxNode)
    {
        return GetMaxNestingDepthCore(syntaxNode, depth: 0);
    }

    private static int GetMaxNestingDepthCore(SyntaxNode syntaxNode, int depth)
    {
        var childDepths = syntaxNode.ChildNodes()
            .Where(IsNestingNode)
            .Select(child => GetMaxNestingDepthCore(child, depth + 1))
            .DefaultIfEmpty(depth);

        return Math.Max(depth, childDepths.Max());
    }

    private static int GetCyclomaticComplexity(SyntaxNode syntaxNode)
    {
        var complexity = 1;
        complexity += syntaxNode.DescendantNodes().Count(static node =>
            node is IfStatementSyntax
                or ForStatementSyntax
                or ForEachStatementSyntax
                or WhileStatementSyntax
                or DoStatementSyntax
                or ConditionalExpressionSyntax
                or CatchClauseSyntax
                or SwitchExpressionArmSyntax
                or CaseSwitchLabelSyntax
                or CasePatternSwitchLabelSyntax);
        complexity += syntaxNode.DescendantTokens().Count(static token =>
            token.IsKind(SyntaxKind.AmpersandAmpersandToken)
            || token.IsKind(SyntaxKind.BarBarToken)
            || token.IsKind(SyntaxKind.QuestionQuestionToken));
        return complexity;
    }

    private static bool IsMetricDeclarationNode(SyntaxNode node)
    {
        return node is BaseTypeDeclarationSyntax
            or BaseMethodDeclarationSyntax
            or LocalFunctionStatementSyntax
            or PropertyDeclarationSyntax
            or EventDeclarationSyntax
            or VariableDeclaratorSyntax;
    }

    private static bool IsNestingNode(SyntaxNode node)
    {
        return node is IfStatementSyntax
            or ForStatementSyntax
            or ForEachStatementSyntax
            or WhileStatementSyntax
            or DoStatementSyntax
            or SwitchStatementSyntax
            or TryStatementSyntax
            or CatchClauseSyntax
            or UsingStatementSyntax
            or LockStatementSyntax;
    }

    private static string NormalizeStatement(StatementSyntax statement)
    {
        return statement.NormalizeWhitespace(elasticTrivia: false).ToFullString().Trim();
    }

    private static int CountLogicalLines(SyntaxNode syntaxNode)
    {
        return syntaxNode
            .ToString()
            .Split(Environment.NewLine, StringSplitOptions.None)
            .Select(static line => line.Trim())
            .Count(static line => !string.IsNullOrWhiteSpace(line) && line is not "{" and not "}");
    }

    private static int CountCoupling(ISymbol symbol)
    {
        var types = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        AddType(types, symbol switch
        {
            IMethodSymbol methodSymbol => methodSymbol.ReturnType,
            IPropertySymbol propertySymbol => propertySymbol.Type,
            IFieldSymbol fieldSymbol => fieldSymbol.Type,
            _ => null,
        });

        switch (symbol)
        {
            case IMethodSymbol methodSymbol:
                foreach (var parameter in methodSymbol.Parameters)
                {
                    AddType(types, parameter.Type);
                }

                break;

            case INamedTypeSymbol namedTypeSymbol:
                AddType(types, namedTypeSymbol.BaseType);
                foreach (var interfaceType in namedTypeSymbol.Interfaces)
                {
                    AddType(types, interfaceType);
                }

                break;
        }

        return types.Count;
    }

    private static void AddType(ISet<ITypeSymbol> types, ITypeSymbol? type)
    {
        if (type is not null)
        {
            types.Add(type);
        }
    }

    private static MetricTarget? TryCreateMetricTarget(ISymbol symbol, IQueryContext context)
    {
        var syntaxReference = symbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxReference is null)
        {
            return null;
        }

        var syntaxNode = syntaxReference.GetSyntax();
        var sourceLocation = symbol.Locations.FirstOrDefault(static location => location.IsInSource);
        if (sourceLocation is null)
        {
            return null;
        }

        return new MetricTarget
        {
            SymbolReference = context.Resolver.CreateSymbolReference(symbol),
            Location = context.Resolver.CreateResolvedLocation(sourceLocation),
            LogicalLines = CountLogicalLines(syntaxNode),
            CyclomaticComplexity = GetCyclomaticComplexity(syntaxNode),
            MaxNestingDepth = GetMaxNestingDepth(syntaxNode),
            Coupling = CountCoupling(symbol),
        };
    }

    internal sealed class MetricsResolutionException : Exception
    {
        public MetricsResolutionException(PluginExecutionResult<CodeMetricsData> rejection)
        {
            Rejection = rejection;
        }

        public PluginExecutionResult<CodeMetricsData> Rejection { get; }
    }

    private sealed record DuplicateCandidate
    {
        public string Key { get; init; } = string.Empty;

        public int StatementCount { get; init; }

        public DuplicateCodeOccurrence Occurrence { get; init; } = new();
    }

    private sealed record MetricTarget
    {
        public SymbolReference SymbolReference { get; init; } = new();

        public ResolvedLocation? Location { get; init; }

        public int LogicalLines { get; init; }

        public int CyclomaticComplexity { get; init; }

        public int MaxNestingDepth { get; init; }

        public int Coupling { get; init; }
    }
}
