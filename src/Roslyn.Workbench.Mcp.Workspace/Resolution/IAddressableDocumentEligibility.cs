namespace Roslyn.Workbench.Mcp.Workspace.Resolution;

internal interface IAddressableDocumentEligibility
{
    bool IsAddressable(Document document);
}
