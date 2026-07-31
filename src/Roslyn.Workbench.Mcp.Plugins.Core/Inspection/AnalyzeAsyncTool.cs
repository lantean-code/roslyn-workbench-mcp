namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("analyze-async", "Analyze Async", "Returns supported async antipattern findings for a selected scope.")]
internal sealed class AnalyzeAsyncTool : QueryToolHandler<AnalyzeAsyncRequest, AsyncAnalysisData>
{
    protected override async ValueTask<PluginExecutionResult<AsyncAnalysisData>> ExecuteCoreAsync(AnalyzeAsyncRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var documents = context.ToolExecutionServices.RequestResolver.ResolveDocuments<AsyncAnalysisData>(request.Scope, context);
        if (documents.HasRejection)
        {
            return documents.Rejection;
        }

        var maxResults = request.EffectiveFindingsLimit;
        var findings = new List<AsyncFinding>();
        var typeSymbolCache = new CompilationTypeSymbolCache();
        var hasMore = false;
        foreach (var document in documents.Value.OrderBy(static item => item.FilePath, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            if (syntaxRoot is null || semanticModel is null)
            {
                continue;
            }

            var compilation = semanticModel.Compilation;
            var task = typeSymbolCache.GetTypeByMetadataName(compilation, "System.Threading.Tasks.Task");
            var taskOfT = typeSymbolCache.GetTypeByMetadataName(compilation, "System.Threading.Tasks.Task`1");
            var valueTask = typeSymbolCache.GetTypeByMetadataName(compilation, "System.Threading.Tasks.ValueTask");
            var valueTaskOfT = typeSymbolCache.GetTypeByMetadataName(compilation, "System.Threading.Tasks.ValueTask`1");

            foreach (var methodDeclaration in syntaxRoot.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (semanticModel.GetDeclaredSymbol(methodDeclaration, cancellationToken) is not IMethodSymbol methodSymbol
                    || !methodSymbol.IsAsync)
                {
                    continue;
                }

                if (!GetExecutableDescendants(methodDeclaration).OfType<AwaitExpressionSyntax>().Any())
                {
                    if (findings.Count == maxResults)
                    {
                        hasMore = true;
                        break;
                    }

                    findings.Add(new AsyncFinding
                    {
                        Kind = "AsyncWithoutAwait",
                        Symbol = context.WorkspaceResolver.CreateSymbolReference(methodSymbol),
                        Location = context.WorkspaceResolver.CreateResolvedLocation(methodDeclaration.Identifier.GetLocation()),
                        Message = "The async method does not contain an await expression.",
                    });
                }

                var executableNode = methodDeclaration.Body ?? (SyntaxNode?)methodDeclaration.ExpressionBody?.Expression;
                var rootOperation = executableNode is null ? null : semanticModel.GetOperation(executableNode, cancellationToken);
                if (rootOperation is null)
                {
                    continue;
                }

                foreach (var invocation in GetExecutableOperations(rootOperation).OfType<IInvocationOperation>())
                {
                    if (!ReturnsTaskLike(invocation.Type, task, taskOfT, valueTask, valueTaskOfT)
                        || !IsDiscarded(invocation))
                    {
                        continue;
                    }

                    if (findings.Count == maxResults)
                    {
                        hasMore = true;
                        break;
                    }

                    findings.Add(new AsyncFinding
                    {
                        Kind = "UnawaitedTask",
                        Symbol = context.WorkspaceResolver.CreateSymbolReference(invocation.TargetMethod),
                        Location = context.WorkspaceResolver.CreateResolvedLocation(invocation.Syntax.GetLocation()),
                        Message = "The task-returning invocation is not awaited.",
                    });
                }

                if (hasMore)
                {
                    break;
                }
            }

            if (hasMore)
            {
                break;
            }
        }

        var orderedFindings = findings
            .OrderBy(static finding => finding.Location?.Document?.Path ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static finding => finding.Location?.Span?.Start ?? int.MaxValue)
            .ThenBy(static finding => finding.Kind, StringComparer.Ordinal)
            .ToArray();

        var data = new AsyncAnalysisData
        {
            Findings = BoundedCollection.CreatePrebounded(
                orderedFindings,
                hasMore),
        };

        return PluginExecutionResult.Success(data);
    }

    private static IEnumerable<SyntaxNode> GetExecutableDescendants(MethodDeclarationSyntax methodDeclaration)
    {
        return methodDeclaration.DescendantNodes(static node =>
            node is not AnonymousFunctionExpressionSyntax
                and not LocalFunctionStatementSyntax);
    }

    private static IEnumerable<IOperation> GetExecutableOperations(IOperation rootOperation)
    {
        var pending = new Stack<IOperation>();
        pending.Push(rootOperation);
        while (pending.Count > 0)
        {
            var operation = pending.Pop();
            yield return operation;
            if (operation is IAnonymousFunctionOperation or ILocalFunctionOperation)
            {
                continue;
            }

            foreach (var child in operation.ChildOperations.Reverse())
            {
                pending.Push(child);
            }
        }
    }

    private static bool IsDiscarded(IInvocationOperation invocation)
    {
        IOperation current = invocation;
        while (current.Parent is IConversionOperation or IParenthesizedOperation)
        {
            current = current.Parent;
        }

        return current.Parent is IExpressionStatementOperation
            || current.Parent is ISimpleAssignmentOperation
            {
                Target: IDiscardOperation,
                Parent: IExpressionStatementOperation,
            };
    }

    private static bool ReturnsTaskLike(
        ITypeSymbol? type,
        INamedTypeSymbol? task,
        INamedTypeSymbol? taskOfT,
        INamedTypeSymbol? valueTask,
        INamedTypeSymbol? valueTaskOfT)
    {
        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        var original = namedType.OriginalDefinition;

        return SymbolEqualityComparer.Default.Equals(original, task)
            || SymbolEqualityComparer.Default.Equals(original, taskOfT)
            || SymbolEqualityComparer.Default.Equals(original, valueTask)
            || SymbolEqualityComparer.Default.Equals(original, valueTaskOfT);
    }
}
