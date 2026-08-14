namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

internal sealed class ResolvedFlowContext
{
    public SyntaxNode SyntaxRoot { get; }

    public SemanticModel SemanticModel { get; }

    public TextSpan SourceSpan { get; }

    public ResolvedLocation ResolvedLocation { get; }

    public ResolvedFlowContext(
        SyntaxNode syntaxRoot,
        SemanticModel semanticModel,
        TextSpan sourceSpan,
        ResolvedLocation resolvedLocation)
    {
        SyntaxRoot = syntaxRoot;
        SemanticModel = semanticModel;
        SourceSpan = sourceSpan;
        ResolvedLocation = resolvedLocation;
    }
}
