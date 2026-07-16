using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

internal sealed record MefHostExportReadResult<T>
{
    public IReadOnlyList<T> Exports { get; init; } = [];

    public string? Error { get; init; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccessful => Error is null;

    public static MefHostExportReadResult<T> Success(IReadOnlyList<T> exports)
    {
        return new MefHostExportReadResult<T>
        {
            Exports = exports,
        };
    }

    public static MefHostExportReadResult<T> Failure(string error)
    {
        return new MefHostExportReadResult<T>
        {
            Error = error,
        };
    }
}
