namespace Roslyn.Workbench.Mcp.CodeActions;

internal sealed class CodeActionMutationRegistration<THandler, TRequest> : IRegisteredCodeActionTool
    where THandler : class, ICodeActionMutationToolHandler<TRequest>
    where TRequest : WorkspaceBoundRequest
{
    public CodeActionMutationRegistration(CodeActionToolMetadata metadata)
    {
        Metadata = metadata;
    }

    public CodeActionToolMetadata Metadata { get; }

    public CodeActionToolKind Kind => CodeActionToolKind.Mutation;

    public Type RequestType => typeof(TRequest);

    public Type ResponseType => typeof(MutationData);

    public TResult Accept<TResult>(ICodeActionToolRegistrationVisitor<TResult> visitor)
    {
        return visitor.VisitMutation(this);
    }
}
