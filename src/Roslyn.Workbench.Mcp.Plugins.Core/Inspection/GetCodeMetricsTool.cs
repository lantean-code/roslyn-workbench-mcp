using Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("get-code-metrics", "Get Code Metrics", "Returns projected code metrics for a scope or symbol.")]
internal sealed class GetCodeMetricsTool : QueryToolHandler<GetCodeMetricsRequest, CodeMetricsData>
{
    protected override async ValueTask<PluginExecutionResult<CodeMetricsData>> ExecuteCoreAsync(GetCodeMetricsRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var metricTargets = new List<MetricTarget>();

        if (request.Symbol is not null)
        {
            var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<CodeMetricsData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
            if (symbolResolution.HasRejection)
            {
                return symbolResolution.Rejection;
            }

            AddMetricTargets(symbolResolution.Value, request.IncludeChildren, metricTargets, context);
        }
        else
        {
            var documents = context.ToolExecutionServices.RequestResolver.ResolveDocuments<CodeMetricsData>(request.Scope, context);
            if (documents.HasRejection)
            {
                return documents.Rejection;
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

        var metrics = metricTargets
            .DistinctBy(static target => target.SymbolReference.DocumentationCommentId ?? target.SymbolReference.DisplayName, StringComparer.Ordinal)
            .OrderBy(static target => target.SymbolReference.DisplayName, StringComparer.Ordinal)
            .Select(CreateMetricInfo)
            .ToArray();

        return PluginExecutionResult<CodeMetricsData>.Success(new CodeMetricsData
        {
            Metrics = ToolExecutionHelpers.CreateBoundedCollection(
                metrics,
                ToolExecutionHelpers.GetMaxResults(context, request.MetricsLimit)),
        });
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

    private static int GetMaxNestingDepth(SyntaxNode syntaxNode)
    {
        return GetMaxNestingDepthCore(syntaxNode, depth: 0);
    }

    private static int GetMaxNestingDepthCore(SyntaxNode syntaxNode, int depth)
    {
        var childDepths = syntaxNode.ChildNodes()
            .Select(child => GetMaxNestingDepthCore(child, IsNestingNode(child) ? depth + 1 : depth))
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
            or DelegateDeclarationSyntax
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
            SymbolReference = context.WorkspaceResolver.CreateSymbolReference(symbol),
            Location = context.WorkspaceResolver.CreateResolvedLocation(sourceLocation),
            LogicalLines = CountLogicalLines(syntaxNode),
            CyclomaticComplexity = GetCyclomaticComplexity(syntaxNode),
            MaxNestingDepth = GetMaxNestingDepth(syntaxNode),
            Coupling = CountCoupling(symbol),
        };
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
