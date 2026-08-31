namespace Roslyn.Workbench.Mcp.CodeActions.Registration;

/// <summary>
/// Exposes a Code Action tool registration to the host without erasing its typed handler contract.
/// </summary>
internal interface IRegisteredCodeActionTool
{
    /// <summary>
    /// Gets the metadata published for the tool.
    /// </summary>
    CodeActionToolMetadata Metadata { get; }

    /// <summary>
    /// Gets whether the tool is a query or mutation.
    /// </summary>
    CodeActionToolKind Kind { get; }

    /// <summary>
    /// Gets the request contract type accepted by the tool.
    /// </summary>
    Type RequestType { get; }

    /// <summary>
    /// Gets the response contract type produced by the tool.
    /// </summary>
    Type ResponseType { get; }

    /// <summary>
    /// Dispatches this registration to a typed visitor.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="visitor">The visitor that handles the registration's concrete query or mutation shape.</param>
    /// <returns>The value produced by the visitor.</returns>
    TResult Accept<TResult>(ICodeActionToolRegistrationVisitor<TResult> visitor);
}
