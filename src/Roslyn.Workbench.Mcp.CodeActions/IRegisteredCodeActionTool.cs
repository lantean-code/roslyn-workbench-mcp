namespace Roslyn.Workbench.Mcp.CodeActions;

internal interface IRegisteredCodeActionTool
{
    CodeActionToolMetadata Metadata { get; }

    CodeActionToolKind Kind { get; }

    Type RequestType { get; }

    Type ResponseType { get; }

    TResult Accept<TResult>(ICodeActionToolRegistrationVisitor<TResult> visitor);
}
