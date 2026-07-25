namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

internal interface ICodeActionReferenceRequest
{
    Guid ActionId { get; }
}
