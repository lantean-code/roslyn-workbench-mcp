namespace Roslyn.Workbench.Mcp.CodeActions;

internal sealed class CodeActionMutationRegistration<TRequest> : IRegisteredCodeActionTool
    where TRequest : WorkspaceBoundRequest
{
    public CodeActionMutationRegistration(
        CodeActionToolMetadata metadata,
        CodeActionMutationToolHandler<TRequest> handler)
    {
        Metadata = metadata;
        Handler = handler;
    }

    public CodeActionToolMetadata Metadata { get; }

    public CodeActionToolKind Kind => CodeActionToolKind.Mutation;

    public Type RequestType => typeof(TRequest);

    public Type ResponseType => typeof(WorkspaceMutationProposal);

    public CodeActionMutationToolHandler<TRequest> Handler { get; }

    public TResult Accept<TResult>(ICodeActionToolRegistrationVisitor<TResult> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);

        return visitor.VisitMutation(this);
    }
}
