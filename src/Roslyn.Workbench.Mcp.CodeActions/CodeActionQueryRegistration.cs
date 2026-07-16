namespace Roslyn.Workbench.Mcp.CodeActions;

internal sealed class CodeActionQueryRegistration<THandler, TRequest, TResponse> : IRegisteredCodeActionTool
    where THandler : class, ICodeActionQueryToolHandler<TRequest, TResponse>
    where TRequest : WorkspaceBoundRequest
{
    public CodeActionQueryRegistration(CodeActionToolMetadata metadata)
    {
        Metadata = metadata;
    }

    public CodeActionToolMetadata Metadata { get; }

    public CodeActionToolKind Kind => CodeActionToolKind.Query;

    public Type RequestType => typeof(TRequest);

    public Type ResponseType => typeof(TResponse);

    public TResult Accept<TResult>(ICodeActionToolRegistrationVisitor<TResult> visitor)
    {
        return visitor.VisitQuery(this);
    }
}
