using Microsoft.CodeAnalysis.CodeActions;

// This fixture must match the fully qualified name of Roslyn's internal wrapping operation.
#pragma warning disable IDE0130

namespace Microsoft.CodeAnalysis.Wrapping;
#pragma warning restore IDE0130

internal static class WrapItemsAction
{
    internal sealed class RecordCodeActionOperation : CodeActionOperation
    {
    }
}
