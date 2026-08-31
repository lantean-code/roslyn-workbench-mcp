using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

/// <summary>
/// Resolves source selections into Roslyn-compatible statement or expression regions for flow analysis.
/// </summary>
internal static class FlowAnalysisRegionResolver
{
    /// <summary>
    /// Resolves a selection that exactly covers one statement or a contiguous statement range.
    /// </summary>
    /// <typeparam name="TResponse">The response type used when projecting a resolution rejection.</typeparam>
    /// <param name="selector">The source location to resolve.</param>
    /// <param name="expectedSnapshot">The optional snapshot that the source location must match.</param>
    /// <param name="context">The active query context.</param>
    /// <param name="cancellationToken">The token that cancels location and semantic resolution.</param>
    /// <returns>The resolved statement region or a typed rejection.</returns>
    public static async ValueTask<ToolResolutionResult<ResolvedStatementFlowRegion, TResponse>> ResolveStatementRegionAsync<TResponse>(
        LocationSelector selector,
        SnapshotPrecondition? expectedSnapshot,
        IQueryContext context,
        CancellationToken cancellationToken)
    {
        var contextResolution = await ResolveContextAsync<TResponse>(selector, expectedSnapshot, context, cancellationToken);
        if (contextResolution.HasRejection)
        {
            return ToolResolutionResult.Rejected<ResolvedStatementFlowRegion, TResponse>(contextResolution.Rejection);
        }

        var resolvedContext = contextResolution.Value;
        if (!TryResolveStatements(resolvedContext.SyntaxRoot, resolvedContext.SourceSpan, out var firstStatement, out var lastStatement))
        {
            var rejection = PluginExecutionResult.Rejected<TResponse>(
                "InvalidRequest",
                "The selected region must exactly match a complete statement or a contiguous range of statements in one executable body.");

            return ToolResolutionResult.Rejected<ResolvedStatementFlowRegion, TResponse>(rejection);
        }

        var region = new ResolvedStatementFlowRegion(
            firstStatement,
            lastStatement,
            resolvedContext.SemanticModel,
            resolvedContext.ResolvedLocation);

        return ToolResolutionResult.Resolved<ResolvedStatementFlowRegion, TResponse>(region);
    }

    /// <summary>
    /// Resolves a selection that exactly covers an expression, one statement, or a contiguous statement range.
    /// </summary>
    /// <typeparam name="TResponse">The response type used when projecting a resolution rejection.</typeparam>
    /// <param name="selector">The source location to resolve.</param>
    /// <param name="expectedSnapshot">The optional snapshot that the source location must match.</param>
    /// <param name="context">The active query context.</param>
    /// <param name="cancellationToken">The token that cancels location and semantic resolution.</param>
    /// <returns>The resolved flow-analysis region or a typed rejection.</returns>
    public static async ValueTask<ToolResolutionResult<ResolvedFlowRegion, TResponse>> ResolveDataFlowRegionAsync<TResponse>(
        LocationSelector selector,
        SnapshotPrecondition? expectedSnapshot,
        IQueryContext context,
        CancellationToken cancellationToken)
    {
        var contextResolution = await ResolveContextAsync<TResponse>(selector, expectedSnapshot, context, cancellationToken);
        if (contextResolution.HasRejection)
        {
            return ToolResolutionResult.Rejected<ResolvedFlowRegion, TResponse>(contextResolution.Rejection);
        }

        var resolvedContext = contextResolution.Value;
        if (TryResolveExpression(resolvedContext.SyntaxRoot, resolvedContext.SourceSpan, out var expression))
        {
            var expressionRegion = new ResolvedExpressionFlowRegion(
                expression,
                resolvedContext.SemanticModel,
                resolvedContext.ResolvedLocation);

            return ToolResolutionResult.Resolved<ResolvedFlowRegion, TResponse>(expressionRegion);
        }

        if (TryResolveStatements(resolvedContext.SyntaxRoot, resolvedContext.SourceSpan, out var firstStatement, out var lastStatement))
        {
            var statementRegion = new ResolvedStatementFlowRegion(
                firstStatement,
                lastStatement,
                resolvedContext.SemanticModel,
                resolvedContext.ResolvedLocation);

            return ToolResolutionResult.Resolved<ResolvedFlowRegion, TResponse>(statementRegion);
        }

        var rejection = PluginExecutionResult.Rejected<TResponse>(
            "InvalidRequest",
            "The selected region must exactly match an expression, a complete statement, or a contiguous range of statements in one executable body.");

        return ToolResolutionResult.Rejected<ResolvedFlowRegion, TResponse>(rejection);
    }

    private static async ValueTask<ToolResolutionResult<ResolvedFlowContext, TResponse>> ResolveContextAsync<TResponse>(
        LocationSelector selector,
        SnapshotPrecondition? expectedSnapshot,
        IQueryContext context,
        CancellationToken cancellationToken)
    {
        var snapshotRejection = context.ToolExecutionServices.RequestResolver.ValidateSnapshot<TResponse>(context, expectedSnapshot);
        if (snapshotRejection is not null)
        {
            return ToolResolutionResult.Rejected<ResolvedFlowContext, TResponse>(snapshotRejection);
        }

        var locationResolution = await context.WorkspaceResolver.ResolveLocationAsync(selector, cancellationToken);
        if (!locationResolution.IsResolved)
        {
            var rejection = SelectorRejectionFactory.Create<TResponse>(
                locationResolution.Status,
                "Location",
                "location");

            return ToolResolutionResult.Rejected<ResolvedFlowContext, TResponse>(rejection);
        }

        var location = locationResolution.Value;
        var resolvedLocation = context.WorkspaceResolver.CreateResolvedLocation(location);
        if (resolvedLocation?.Document?.Path is null)
        {
            return ToolResolutionResult.Rejected<ResolvedFlowContext, TResponse>(CreateLocationNotFoundRejection<TResponse>());
        }

        var document = location.SourceTree is null
            ? null
            : context.CurrentSolution.GetDocument(location.SourceTree);

        if (document is null)
        {
            return ToolResolutionResult.Rejected<ResolvedFlowContext, TResponse>(CreateLocationNotFoundRejection<TResponse>());
        }

        var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (syntaxRoot is null || semanticModel is null)
        {
            return ToolResolutionResult.Rejected<ResolvedFlowContext, TResponse>(CreateLocationNotFoundRejection<TResponse>());
        }

        var resolvedContext = new ResolvedFlowContext(
            syntaxRoot,
            semanticModel,
            location.SourceSpan,
            resolvedLocation);

        return ToolResolutionResult.Resolved<ResolvedFlowContext, TResponse>(resolvedContext);
    }

    private static bool TryResolveExpression(SyntaxNode syntaxRoot, TextSpan sourceSpan, [NotNullWhen(true)] out ExpressionSyntax? expression)
    {
        expression = syntaxRoot
            .FindNode(sourceSpan, getInnermostNodeForTie: true)
            .AncestorsAndSelf()
            .OfType<ExpressionSyntax>()
            .FirstOrDefault(candidate => candidate.Span == sourceSpan);

        return expression is not null;
    }

    private static bool TryResolveStatements(
        SyntaxNode syntaxRoot,
        TextSpan sourceSpan,
        [NotNullWhen(true)] out StatementSyntax? firstStatement,
        [NotNullWhen(true)] out StatementSyntax? lastStatement)
    {
        firstStatement = null;
        lastStatement = null;
        if (sourceSpan.IsEmpty)
        {
            return false;
        }

        var exactStatement = syntaxRoot
            .FindNode(sourceSpan, getInnermostNodeForTie: true)
            .AncestorsAndSelf()
            .OfType<StatementSyntax>()
            .FirstOrDefault(candidate => candidate.Span == sourceSpan);

        if (exactStatement is not null)
        {
            firstStatement = exactStatement;
            lastStatement = exactStatement;
            return true;
        }

        var firstParent = syntaxRoot.FindToken(sourceSpan.Start).Parent;
        var lastParent = syntaxRoot.FindToken(sourceSpan.End - 1).Parent;
        if (firstParent is null || lastParent is null)
        {
            return false;
        }

        var firstCandidates = firstParent
            .AncestorsAndSelf()
            .OfType<StatementSyntax>()
            .Where(candidate => candidate.SpanStart == sourceSpan.Start)
            .ToArray();

        var lastCandidates = lastParent
            .AncestorsAndSelf()
            .OfType<StatementSyntax>()
            .Where(candidate => candidate.Span.End == sourceSpan.End)
            .ToArray();

        foreach (var firstCandidate in firstCandidates)
        {
            var siblings = GetSiblingStatements(firstCandidate);
            var firstIndex = IndexOf(siblings, firstCandidate);
            foreach (var lastCandidate in lastCandidates)
            {
                var lastIndex = IndexOf(siblings, lastCandidate);
                if (lastIndex >= firstIndex)
                {
                    firstStatement = firstCandidate;
                    lastStatement = lastCandidate;
                    return true;
                }
            }
        }

        return false;
    }

    private static StatementSyntax[] GetSiblingStatements(StatementSyntax statement)
    {
        return statement.Parent switch
        {
            BlockSyntax block => block.Statements.ToArray(),
            SwitchSectionSyntax switchSection => switchSection.Statements.ToArray(),
            GlobalStatementSyntax { Parent: CompilationUnitSyntax compilationUnit } globalStatement => GetContiguousGlobalStatements(
                globalStatement,
                compilationUnit),
            _ => [statement],
        };
    }

    private static StatementSyntax[] GetContiguousGlobalStatements(
        GlobalStatementSyntax globalStatement,
        CompilationUnitSyntax compilationUnit)
    {
        var members = compilationUnit.Members;
        var statementIndex = members.IndexOf(globalStatement);
        var firstIndex = statementIndex;
        while (firstIndex > 0 && members[firstIndex - 1] is GlobalStatementSyntax)
        {
            firstIndex--;
        }

        var lastIndex = statementIndex;
        while (lastIndex + 1 < members.Count && members[lastIndex + 1] is GlobalStatementSyntax)
        {
            lastIndex++;
        }

        var statements = new StatementSyntax[lastIndex - firstIndex + 1];
        for (var index = firstIndex; index <= lastIndex; index++)
        {
            statements[index - firstIndex] = ((GlobalStatementSyntax)members[index]).Statement;
        }

        return statements;
    }

    private static int IndexOf(StatementSyntax[] statements, StatementSyntax statement)
    {
        for (var index = 0; index < statements.Length; index++)
        {
            if (statements[index] == statement)
            {
                return index;
            }
        }

        return -1;
    }

    private static PluginExecutionResult<TResponse> CreateLocationNotFoundRejection<TResponse>()
    {
        return PluginExecutionResult.Rejected<TResponse>(
            "LocationNotFound",
            "The location selector did not resolve to a source document.",
            RequiredAction.ResolveTargetAgain);
    }

}
