namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("get-code-metrics", "Get Code Metrics", "Returns projected code metrics for a scope or symbol.")]
internal sealed class GetCodeMetricsTool : QueryToolHandler<GetCodeMetricsRequest, CodeMetricsData>
{
    protected override async ValueTask<PluginExecutionResult<CodeMetricsData>> ExecuteCoreAsync(GetCodeMetricsRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var metricCandidates = new List<MetricCandidate>();

        if (request.Symbol is not null)
        {
            var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<CodeMetricsData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken);
            if (symbolResolution.HasRejection)
            {
                return symbolResolution.Rejection;
            }

            AddMetricCandidates(symbolResolution.Value, request.IncludeChildren, metricCandidates);
        }
        else
        {
            var documents = context.ToolExecutionServices.RequestResolver.ResolveDocuments<CodeMetricsData>(request.Scope, context);
            if (documents.HasRejection)
            {
                return documents.Rejection;
            }

            var orderedDocuments = new List<Document>(documents.Value);
            orderedDocuments.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.FilePath, right.FilePath));
            foreach (var document in orderedDocuments)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
                var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
                if (syntaxRoot is null || semanticModel is null)
                {
                    continue;
                }

                foreach (var declaration in syntaxRoot.DescendantNodes())
                {
                    if (!IsMetricDeclarationNode(declaration))
                    {
                        continue;
                    }

                    if (GetDeclaredSymbol(semanticModel, declaration, cancellationToken) is { } symbol && !symbol.IsImplicitlyDeclared)
                    {
                        AddMetricCandidates(symbol, includeChildren: false, metricCandidates);
                    }
                }
            }
        }

        var maxResults = request.EffectiveMetricsLimit;
        var uniqueCandidates = new Dictionary<string, MetricCandidate>(StringComparer.Ordinal);
        foreach (var candidate in metricCandidates)
        {
            uniqueCandidates.TryAdd(candidate.Identity, candidate);
        }

        var orderedCandidates = new List<MetricCandidate>(uniqueCandidates.Values);
        orderedCandidates.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.DisplayName, right.DisplayName));

        var metrics = new List<MetricInfo>();
        foreach (var candidate in orderedCandidates)
        {
            if (metrics.Count == maxResults)
            {
                break;
            }

            metrics.Add(CreateMetricInfo(candidate, context));
        }

        var data = new CodeMetricsData
        {
            Metrics = BoundedCollection.CreatePrebounded(metrics, orderedCandidates.Count),
        };

        return PluginExecutionResult.Success(data);
    }

    private static void AddMetricCandidates(ISymbol symbol, bool includeChildren, ICollection<MetricCandidate> candidates)
    {
        if (TryCreateMetricCandidate(symbol) is { } candidate)
        {
            candidates.Add(candidate);
        }

        if (!includeChildren || symbol is not INamedTypeSymbol namedType)
        {
            return;
        }

        foreach (var child in namedType.GetMembers())
        {
            if (child.IsImplicitlyDeclared)
            {
                continue;
            }

            if (TryCreateMetricCandidate(child) is { } childCandidate)
            {
                candidates.Add(childCandidate);
            }
        }
    }

    private static MetricInfo CreateMetricInfo(MetricCandidate candidate, IQueryContext context)
    {
        var syntaxNode = candidate.SyntaxReference.GetSyntax();
        var logicalLines = CountLogicalLines(syntaxNode);
        var cyclomaticComplexity = GetCyclomaticComplexity(syntaxNode);
        var maxNestingDepth = GetMaxNestingDepth(syntaxNode);
        var coupling = CountCoupling(candidate.Symbol);
        var maintainabilityIndex = Math.Clamp(100 - (cyclomaticComplexity * 5) - logicalLines - (maxNestingDepth * 3) - (coupling * 2), 0, 100);

        return new MetricInfo
        {
            Symbol = context.WorkspaceResolver.CreateSymbolReference(candidate.Symbol),
            Location = context.WorkspaceResolver.CreateResolvedLocation(candidate.SourceLocation),
            LogicalLines = logicalLines,
            CyclomaticComplexity = cyclomaticComplexity,
            MaxNestingDepth = maxNestingDepth,
            Coupling = coupling,
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
        var maxDepth = depth;
        foreach (var child in syntaxNode.ChildNodes())
        {
            var childDepth = IsNestingNode(child) ? depth + 1 : depth;
            maxDepth = Math.Max(maxDepth, GetMaxNestingDepthCore(child, childDepth));
        }

        return maxDepth;
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
        var lines = syntaxNode.ToString().Split(Environment.NewLine, StringSplitOptions.None);
        var logicalLines = 0;
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedLine) && trimmedLine is not "{" and not "}")
            {
                logicalLines++;
            }
        }

        return logicalLines;
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

    private static void AddType(HashSet<ITypeSymbol> types, ITypeSymbol? type)
    {
        if (type is not null)
        {
            types.Add(type);
        }
    }

    private static MetricCandidate? TryCreateMetricCandidate(ISymbol symbol)
    {
        var syntaxReference = symbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxReference is null)
        {
            return null;
        }

        var sourceLocation = symbol.Locations.FirstOrDefault(static location => location.IsInSource);
        if (sourceLocation is null)
        {
            return null;
        }

        var displayName = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        return new MetricCandidate(
            symbol,
            symbol.GetDocumentationCommentId() ?? displayName,
            displayName,
            syntaxReference,
            sourceLocation);
    }

    private sealed record MetricCandidate
    {
        public ISymbol Symbol { get; }

        public string Identity { get; }

        public string DisplayName { get; }

        public SyntaxReference SyntaxReference { get; }

        public Location SourceLocation { get; }

        public MetricCandidate(
            ISymbol symbol,
            string identity,
            string displayName,
            SyntaxReference syntaxReference,
            Location sourceLocation)
        {
            Symbol = symbol;
            Identity = identity;
            DisplayName = displayName;
            SyntaxReference = syntaxReference;
            SourceLocation = sourceLocation;
        }
    }
}
