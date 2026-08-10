using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal sealed class CodeActionInfoCreationResult
{
    public CodeActionInfoCreationStatus Status { get; }

    public CodeActionListItem? Item { get; }

    [MemberNotNullWhen(true, nameof(Item))]
    public bool IsSucceeded => Status == CodeActionInfoCreationStatus.Succeeded;

    private CodeActionInfoCreationResult(
        CodeActionInfoCreationStatus status,
        CodeActionListItem? item)
    {
        Status = status;
        Item = item;
    }

    public static CodeActionInfoCreationResult Success(CodeActionListItem item)
    {
        return new CodeActionInfoCreationResult(CodeActionInfoCreationStatus.Succeeded, item);
    }

    public static CodeActionInfoCreationResult LocationUnavailable()
    {
        return new CodeActionInfoCreationResult(CodeActionInfoCreationStatus.LocationUnavailable, item: null);
    }

    public static CodeActionInfoCreationResult DocumentPathUnavailable()
    {
        return new CodeActionInfoCreationResult(CodeActionInfoCreationStatus.DocumentPathUnavailable, item: null);
    }

    public static CodeActionInfoCreationResult ReferenceCapacityExceeded()
    {
        return new CodeActionInfoCreationResult(CodeActionInfoCreationStatus.ReferenceCapacityExceeded, item: null);
    }
}
