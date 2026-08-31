namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

/// <summary>
/// Reports whether Code Action composition succeeded and why it is unavailable when it did not.
/// </summary>
internal sealed record CodeActionCompositionStatus
{
    /// <summary>
    /// Gets a value indicating whether the requested capability is available.
    /// </summary>
    public bool IsAvailable { get; }

    /// <summary>
    /// Gets the Roslyn workspace assembly version, when composition succeeded.
    /// </summary>
    public string? Version { get; }

    /// <summary>
    /// Gets the composition summary or failure explanation.
    /// </summary>
    public string? Message { get; }

    private CodeActionCompositionStatus(
        bool isAvailable,
        string? version,
        string? message)
    {
        IsAvailable = isAvailable;
        Version = version;
        Message = message;
    }

    /// <summary>
    /// Creates a status for successful composition.
    /// </summary>
    /// <param name="version">The version used to identify the relevant state.</param>
    /// <param name="message">The message that describes the reported condition.</param>
    /// <returns>The Code Action composition status.</returns>
    public static CodeActionCompositionStatus Available(
        string? version = null,
        string? message = null)
    {
        return new CodeActionCompositionStatus(
            isAvailable: true,
            version,
            message);
    }

    /// <summary>
    /// Creates a status for failed or disabled composition.
    /// </summary>
    /// <param name="message">The message that describes the reported condition.</param>
    /// <returns>The Code Action composition status.</returns>
    public static CodeActionCompositionStatus Unavailable(string message)
    {
        return new CodeActionCompositionStatus(
            isAvailable: false,
            version: null,
            message);
    }
}
