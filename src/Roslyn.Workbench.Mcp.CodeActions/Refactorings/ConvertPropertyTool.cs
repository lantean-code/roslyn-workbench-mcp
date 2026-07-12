using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class ConvertPropertyTool : CodeActionMutationToolHandler<ConvertPropertyRequest>
{
    private const string ConvertToFullProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertAutoPropertyToFullProperty.CSharpConvertAutoPropertyToFullPropertyCodeRefactoringProvider";
    private const string UseAutoPropertyProviderId = "Microsoft.CodeAnalysis.CSharp.UseAutoProperty.CSharpUseAutoPropertyCodeFixProvider";
    private const string UseAutoPropertyAnalyzerTypeName = "Microsoft.CodeAnalysis.CSharp.UseAutoProperty.CSharpUseAutoPropertyAnalyzer";
    private const string UseAutoPropertyDiagnosticId = "IDE0032";

    private static readonly CodeActionToolMetadata _metadata = new()
    {
        Name = "convert-property",
        Title = "Convert Property",
        Description = "Converts one selected property between supported auto-property and full-property forms through Roslyn composition.",
        Behavior = new CodeActionToolBehavior
        {
            Destructive = true,
        },
    };

    public static void Register(ICodeActionToolRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new ConvertPropertyTool());
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(ConvertPropertyRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        return request.Direction switch
        {
            ConvertPropertyDirection.ToFull => context.StageReplaySelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                                cancellationToken,
                ConvertToFullProviderId,
                title: "Convert to full property"),
            ConvertPropertyDirection.ToAutoWhenSafe => context.StageLocationCodeFixAsync(new LocationCodeFixRequest
            {
                Location = request.Selection,
                ExpectedSnapshot = request.ExpectedSnapshot,
                DiagnosticIds = [UseAutoPropertyDiagnosticId],
                ProviderId = UseAutoPropertyProviderId,
                Title = "Use auto property",
                AnalyzerTypeName = UseAutoPropertyAnalyzerTypeName,
                SyntheticDiagnosticId = UseAutoPropertyDiagnosticId,
            }, cancellationToken),
            _ => ValueTask.FromResult(ToolExecutionHelpers.Rejected<WorkspaceMutationCandidate>("InvalidRequest", "The requested property conversion direction is not supported.")),
        };
    }
}
