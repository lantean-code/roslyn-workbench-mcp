using Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

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

        var findings = new List<AsyncFinding>();
        foreach (var document in documents.Value.OrderBy(static item => item.FilePath, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (syntaxRoot is null || semanticModel is null)
            {
                continue;
            }

            foreach (var methodDeclaration in syntaxRoot.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (semanticModel.GetDeclaredSymbol(methodDeclaration, cancellationToken) is not IMethodSymbol methodSymbol
                    || !methodSymbol.IsAsync)
                {
                    continue;
                }

                if (!methodDeclaration.DescendantNodes().OfType<AwaitExpressionSyntax>().Any())
                {
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

                foreach (var invocation in rootOperation.DescendantsAndSelf().OfType<IInvocationOperation>())
                {
                    if (!ReturnsTaskLike(invocation.Type, semanticModel.Compilation) || IsAwaited(invocation))
                    {
                        continue;
                    }

                    findings.Add(new AsyncFinding
                    {
                        Kind = "UnawaitedTask",
                        Symbol = context.WorkspaceResolver.CreateSymbolReference(invocation.TargetMethod),
                        Location = context.WorkspaceResolver.CreateResolvedLocation(invocation.Syntax.GetLocation()),
                        Message = "The task-returning invocation is not awaited.",
                    });
                }
            }
        }

        var orderedFindings = findings
            .OrderBy(static finding => finding.Location?.Document?.Path ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static finding => finding.Location?.Span?.Start ?? int.MaxValue)
            .ThenBy(static finding => finding.Kind, StringComparer.Ordinal)
            .ToArray();

        return PluginExecutionResult<AsyncAnalysisData>.Success(new AsyncAnalysisData
        {
            Findings = ToolExecutionHelpers.CreateBoundedCollection(
                orderedFindings,
                ToolExecutionHelpers.GetMaxResults(context, request.FindingsLimit)),
        });
    }

    private static bool IsAwaited(IOperation operation)
    {
        for (var current = operation.Parent; current is not null; current = current.Parent)
        {
            if (current is IAwaitOperation)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ReturnsTaskLike(ITypeSymbol? type, Compilation compilation)
    {
        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        var task = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
        var taskOfT = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");
        var valueTask = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask");
        var valueTaskOfT = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1");
        var original = namedType.OriginalDefinition;

        return SymbolEqualityComparer.Default.Equals(original, task)
            || SymbolEqualityComparer.Default.Equals(original, taskOfT)
            || SymbolEqualityComparer.Default.Equals(original, valueTask)
            || SymbolEqualityComparer.Default.Equals(original, valueTaskOfT);
    }
}
