namespace Roslyn.Workbench.Mcp.CodeActions.Registration;

/// <summary>
/// Associates a Code Action query handler and its contracts with published metadata.
/// </summary>
/// <typeparam name="THandler">The handler type.</typeparam>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
internal sealed class CodeActionQueryRegistration<THandler, TRequest, TResponse> : IRegisteredCodeActionTool
    where THandler : class, ICodeActionQueryToolHandler<TRequest, TResponse>
    where TRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CodeActionQueryRegistration{THandler, TRequest, TResponse}"/> class.
    /// </summary>
    /// <param name="metadata">The metadata published for the tool.</param>
    public CodeActionQueryRegistration(CodeActionToolMetadata metadata)
    {
        Metadata = metadata;
    }

    /// <summary>
    /// Gets the metadata published for the tool.
    /// </summary>
    public CodeActionToolMetadata Metadata { get; }

    /// <summary>
    /// Gets the tool's query classification.
    /// </summary>
    public CodeActionToolKind Kind => CodeActionToolKind.Query;

    /// <summary>
    /// Gets the request contract type accepted by the handler.
    /// </summary>
    public Type RequestType => typeof(TRequest);

    /// <summary>
    /// Gets the response contract type produced by the handler.
    /// </summary>
    public Type ResponseType => typeof(TResponse);

    /// <inheritdoc/>
    public TResult Accept<TResult>(ICodeActionToolRegistrationVisitor<TResult> visitor)
    {
        return visitor.VisitQuery(this);
    }
}
