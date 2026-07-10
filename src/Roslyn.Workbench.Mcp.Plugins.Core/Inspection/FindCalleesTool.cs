using Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

internal sealed class FindCalleesTool : QueryToolHandler<FindCalleesRequest, CalleeSearchData>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "find-callees",
        Title = "Find Callees",
        Description = "Returns symbols directly invoked by a method or selected executable body.",
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new FindCalleesTool());
    }

    protected override async ValueTask<PluginExecutionResult<CalleeSearchData>> ExecuteCoreAsync(FindCalleesRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        if (request.Symbol is null == request.Location is null)
        {
            return ToolExecutionHelpers.Rejected<CalleeSearchData>("InvalidRequest", "Specify exactly one of symbol or location.");
        }

        var directCallees = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        ISymbol sourceSymbol;

        if (request.Symbol is not null)
        {
            var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<CalleeSearchData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
            if (symbolResolution.HasRejection)
            {
                return symbolResolution.Rejection;
            }

            sourceSymbol = symbolResolution.Value;
            var foundSourceOperation = false;
            foreach (var syntaxReference in sourceSymbol.DeclaringSyntaxReferences)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var syntax = await syntaxReference.GetSyntaxAsync(cancellationToken).ConfigureAwait(false);
                if (context.CurrentSolution.GetDocument(syntax.SyntaxTree) is not { } document)
                {
                    continue;
                }

                var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
                if (semanticModel is null)
                {
                    continue;
                }

                var executableNode = GetExecutableNode(syntax);
                if (executableNode is null)
                {
                    continue;
                }

                var operation = semanticModel.GetOperation(executableNode, cancellationToken);
                if (operation is null)
                {
                    continue;
                }

                foundSourceOperation = true;
                AddDirectCallees(operation, directCallees);
            }

            if (!foundSourceOperation)
            {
                return ToolExecutionHelpers.Rejected<CalleeSearchData>("InvalidRequest", "The selected symbol does not have an executable source body.");
            }
        }
        else
        {
            var locationResolution = await ResolveLocationAsync(request.Location, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
            if (locationResolution.Rejection is not null)
            {
                return locationResolution.Rejection;
            }

            var operation = GetOperation(locationResolution.SemanticModel!, locationResolution.Node!, cancellationToken);
            if (operation is null)
            {
                return ToolExecutionHelpers.Rejected<CalleeSearchData>("InvalidRequest", "The selected location does not resolve to executable code.");
            }

            sourceSymbol = locationResolution.SemanticModel!.GetEnclosingSymbol(locationResolution.Node!.SpanStart, cancellationToken)!;
            AddDirectCallees(operation, directCallees);
        }

        if (request.IncludeIndirect)
        {
            await ExpandIndirectCalleesAsync(directCallees, context, cancellationToken).ConfigureAwait(false);
        }

        var orderedCallees = directCallees
            .OrderBy(symbol => context.WorkspaceResolver.CreateSymbolReference(symbol).DisplayName, StringComparer.Ordinal)
            .Select(context.WorkspaceResolver.CreateSymbolReference)
            .ToArray();

        return PluginExecutionResult<CalleeSearchData>.Success(new CalleeSearchData
        {
            Source = context.WorkspaceResolver.CreateSymbolReference(sourceSymbol),
            Callees = ToolExecutionHelpers.CreateBoundedCollection(
                orderedCallees,
                ToolExecutionHelpers.GetMaxResults(context, request.CalleesLimit)),
        });
    }

    private static void AddDirectCallees(IOperation operation, ISet<ISymbol> callees)
    {
        foreach (var descendant in operation.DescendantsAndSelf())
        {
            switch (descendant)
            {
                case IInvocationOperation invocationOperation:
                    callees.Add(invocationOperation.TargetMethod);
                    break;

                case IObjectCreationOperation objectCreationOperation when objectCreationOperation.Constructor is not null:
                    callees.Add(objectCreationOperation.Constructor);
                    break;
            }
        }
    }

    private static async ValueTask ExpandIndirectCalleesAsync(ISet<ISymbol> callees, IQueryContext context, CancellationToken cancellationToken)
    {
        var visited = new HashSet<ISymbol>(callees, SymbolEqualityComparer.Default);
        var pending = new Queue<ISymbol>(callees);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var symbol = pending.Dequeue();
            foreach (var syntaxReference in symbol.DeclaringSyntaxReferences)
            {
                var syntax = await syntaxReference.GetSyntaxAsync(cancellationToken).ConfigureAwait(false);
                if (context.CurrentSolution.GetDocument(syntax.SyntaxTree) is not { } document)
                {
                    continue;
                }

                var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
                if (semanticModel is null)
                {
                    continue;
                }

                var executableNode = GetExecutableNode(syntax);
                if (executableNode is null)
                {
                    continue;
                }

                var operation = semanticModel.GetOperation(executableNode, cancellationToken);
                if (operation is null)
                {
                    continue;
                }

                var nestedCallees = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
                AddDirectCallees(operation, nestedCallees);
                foreach (var nested in nestedCallees)
                {
                    if (visited.Add(nested))
                    {
                        callees.Add(nested);
                        pending.Enqueue(nested);
                    }
                }
            }
        }
    }

    private static IOperation? GetOperation(SemanticModel semanticModel, SyntaxNode node, CancellationToken cancellationToken)
    {
        for (var current = node; current is not null; current = current.Parent)
        {
            var operation = semanticModel.GetOperation(current, cancellationToken);
            if (operation is not null)
            {
                return operation;
            }
        }

        return null;
    }

    private static CSharpSyntaxNode? GetExecutableNode(SyntaxNode node)
    {
        return node switch
        {
            BaseMethodDeclarationSyntax { Body: not null } method => method.Body,
            BaseMethodDeclarationSyntax { ExpressionBody: not null } method => method.ExpressionBody.Expression,
            LocalFunctionStatementSyntax { Body: not null } localFunction => localFunction.Body,
            LocalFunctionStatementSyntax { ExpressionBody: not null } localFunction => localFunction.ExpressionBody.Expression,
            AccessorDeclarationSyntax { Body: not null } accessor => accessor.Body,
            AccessorDeclarationSyntax { ExpressionBody: not null } accessor => accessor.ExpressionBody.Expression,
            AnonymousFunctionExpressionSyntax anonymousFunction => anonymousFunction.Body,
            _ => null,
        };
    }

    private static async ValueTask<LocationResolution> ResolveLocationAsync(LocationSelector? selector, SnapshotPrecondition? expectedSnapshot, IQueryContext context, CancellationToken cancellationToken)
    {
        var snapshotRejection = context.ToolExecutionServices.RequestResolver.ValidateSnapshot<CalleeSearchData>(context, expectedSnapshot);
        if (snapshotRejection is not null)
        {
            return new LocationResolution
            {
                Rejection = snapshotRejection,
            };
        }

        if (selector is null)
        {
            return new LocationResolution
            {
                Rejection = ToolExecutionHelpers.Rejected<CalleeSearchData>("InvalidRequest", "A location selector is required."),
            };
        }

        var location = await context.WorkspaceResolver.ResolveLocationAsync(selector, cancellationToken).ConfigureAwait(false);
        if (location.Status != SelectorResolveStatus.Resolved)
        {
            return new LocationResolution
            {
                Rejection = ToolExecutionHelpers.RejectFromStatus<CalleeSearchData>(location.Status, "Location"),
            };
        }

        var document = context.CurrentSolution.GetDocument(location.Value!.SourceTree!);
        if (document is null)
        {
            return new LocationResolution
            {
                Rejection = ToolExecutionHelpers.Rejected<CalleeSearchData>("LocationNotFound", "The location selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain),
            };
        }

        var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (syntaxRoot is null || semanticModel is null)
        {
            return new LocationResolution
            {
                Rejection = ToolExecutionHelpers.Rejected<CalleeSearchData>("LocationNotFound", "The location selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain),
            };
        }

        return new LocationResolution
        {
            Node = syntaxRoot.FindNode(location.Value.SourceSpan, getInnermostNodeForTie: true),
            SemanticModel = semanticModel,
        };
    }

    private sealed record LocationResolution
    {
        public PluginExecutionResult<CalleeSearchData>? Rejection { get; init; }

        public SyntaxNode? Node { get; init; }

        public SemanticModel? SemanticModel { get; init; }
    }
}
