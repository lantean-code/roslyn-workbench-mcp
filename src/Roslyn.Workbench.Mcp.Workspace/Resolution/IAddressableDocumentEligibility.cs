namespace Roslyn.Workbench.Mcp.Workspace.Resolution;

/// <summary>
/// Determines which loaded documents may be exposed to agent-facing selection and mutation.
/// </summary>
internal interface IAddressableDocumentEligibility
{
    /// <summary>
    /// Determines whether a document is eligible for agent-facing selection and mutation.
    /// </summary>
    /// <param name="document">The document to classify.</param>
    /// <returns><see langword="true"/> when the document may be selected or mutated; otherwise, <see langword="false"/>.</returns>
    bool IsAddressable(Document document);
}
