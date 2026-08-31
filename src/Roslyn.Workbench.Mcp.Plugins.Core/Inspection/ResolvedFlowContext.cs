namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

/// <summary>
/// Holds the syntax, semantic model, span, and canonical location resolved for a flow-analysis request.
/// </summary>
internal sealed class ResolvedFlowContext
{
    /// <summary>
    /// Gets the syntax root.
    /// </summary>
    public SyntaxNode SyntaxRoot { get; }

    /// <summary>
    /// Gets the semantic model.
    /// </summary>
    public SemanticModel SemanticModel { get; }

    /// <summary>
    /// Gets the source span.
    /// </summary>
    public TextSpan SourceSpan { get; }

    /// <summary>
    /// Gets the resolved location.
    /// </summary>
    public ResolvedLocation ResolvedLocation { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ResolvedFlowContext"/> class.
    /// </summary>
    /// <param name="syntaxRoot">The root of the selected document's syntax tree.</param>
    /// <param name="semanticModel">The semantic model for the selected document.</param>
    /// <param name="sourceSpan">The exact source span selected for analysis.</param>
    /// <param name="resolvedLocation">The canonical location corresponding to the selection.</param>
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
