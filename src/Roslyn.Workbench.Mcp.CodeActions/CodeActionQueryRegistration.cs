namespace Roslyn.Workbench.Mcp.CodeActions;

internal sealed class CodeActionQueryRegistration<TRequest, TResponse> : IRegisteredCodeActionTool
    where TRequest : WorkspaceBoundRequest
{
    public CodeActionQueryRegistration(
        CodeActionToolMetadata metadata,
        CodeActionQueryToolHandler<TRequest, TResponse> handler)
    {
        Metadata = metadata;
        Handler = handler;
    }

    public CodeActionToolMetadata Metadata { get; }

    public CodeActionToolKind Kind => CodeActionToolKind.Query;

    public Type RequestType => typeof(TRequest);

    public Type ResponseType => typeof(TResponse);

    public CodeActionQueryToolHandler<TRequest, TResponse> Handler { get; }

    public TResult Accept<TResult>(ICodeActionToolRegistrationVisitor<TResult> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);

        return visitor.VisitQuery(this);
    }
}
