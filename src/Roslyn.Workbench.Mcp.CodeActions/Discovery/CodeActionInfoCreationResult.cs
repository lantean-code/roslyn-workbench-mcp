using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

/// <summary>
/// Represents a published Code Action item or the reason it could not be created.
/// </summary>
internal sealed class CodeActionInfoCreationResult
{
    /// <summary>
    /// Gets the item-creation outcome.
    /// </summary>
    public CodeActionInfoCreationStatus Status { get; }

    /// <summary>
    /// Gets the published item when creation succeeded.
    /// </summary>
    public CodeActionListItem? Item { get; }

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Item))]
    public bool IsSucceeded => Status == CodeActionInfoCreationStatus.Succeeded;

    private CodeActionInfoCreationResult(
        CodeActionInfoCreationStatus status,
        CodeActionListItem? item)
    {
        Status = status;
        Item = item;
    }

    /// <summary>
    /// Creates a successful item-creation result.
    /// </summary>
    /// <param name="item">The Code Action metadata created successfully.</param>
    /// <returns>A result that represents successful completion.</returns>
    public static CodeActionInfoCreationResult Success(CodeActionListItem item)
    {
        return new CodeActionInfoCreationResult(CodeActionInfoCreationStatus.Succeeded, item);
    }

    /// <summary>
    /// Creates a result that represents an unavailable source location.
    /// </summary>
    /// <returns>A result that represents an unavailable source location.</returns>
    public static CodeActionInfoCreationResult LocationUnavailable()
    {
        return new CodeActionInfoCreationResult(CodeActionInfoCreationStatus.LocationUnavailable, item: null);
    }

    /// <summary>
    /// Creates a result that represents an unavailable document path.
    /// </summary>
    /// <returns>A result that represents an unavailable document path.</returns>
    public static CodeActionInfoCreationResult DocumentPathUnavailable()
    {
        return new CodeActionInfoCreationResult(CodeActionInfoCreationStatus.DocumentPathUnavailable, item: null);
    }

    /// <summary>
    /// Creates a result that represents reference-capacity exhaustion.
    /// </summary>
    /// <returns>A result that represents reference-capacity exhaustion.</returns>
    public static CodeActionInfoCreationResult ReferenceCapacityExceeded()
    {
        return new CodeActionInfoCreationResult(CodeActionInfoCreationStatus.ReferenceCapacityExceeded, item: null);
    }
}
