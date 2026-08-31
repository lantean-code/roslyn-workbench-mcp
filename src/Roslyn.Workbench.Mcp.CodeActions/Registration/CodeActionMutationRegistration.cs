namespace Roslyn.Workbench.Mcp.CodeActions.Registration;

/// <summary>
/// Associates a Code Action mutation handler and request contract with its published metadata.
/// </summary>
/// <typeparam name="THandler">The handler type.</typeparam>
/// <typeparam name="TRequest">The request type.</typeparam>
internal sealed class CodeActionMutationRegistration<THandler, TRequest> : IRegisteredCodeActionTool
    where THandler : class, ICodeActionMutationToolHandler<TRequest>
    where TRequest : WorkspaceMutationRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CodeActionMutationRegistration{THandler, TRequest}"/> class.
    /// </summary>
    /// <param name="metadata">The metadata published for the tool.</param>
    public CodeActionMutationRegistration(CodeActionToolMetadata metadata)
    {
        Metadata = metadata;
    }

    /// <summary>
    /// Gets the metadata published for the tool.
    /// </summary>
    public CodeActionToolMetadata Metadata { get; }

    /// <summary>
    /// Gets the tool's mutation classification.
    /// </summary>
    public CodeActionToolKind Kind => CodeActionToolKind.Mutation;

    /// <summary>
    /// Gets the request contract type accepted by the handler.
    /// </summary>
    public Type RequestType => typeof(TRequest);

    /// <summary>
    /// Gets the standard mutation response type.
    /// </summary>
    public Type ResponseType => typeof(MutationData);

    /// <inheritdoc/>
    public TResult Accept<TResult>(ICodeActionToolRegistrationVisitor<TResult> visitor)
    {
        return visitor.VisitMutation(this);
    }
}
