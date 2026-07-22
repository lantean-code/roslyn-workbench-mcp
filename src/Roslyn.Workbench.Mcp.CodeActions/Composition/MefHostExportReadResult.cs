using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

internal sealed record MefHostExportReadResult<T>
{
    public IReadOnlyList<T> Exports { get; }

    public string? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccessful => Error is null;

    private MefHostExportReadResult(IReadOnlyList<T> exports, string? error)
    {
        Exports = exports;
        Error = error;
    }

    public static MefHostExportReadResult<T> Success(IReadOnlyList<T> exports)
    {
        return new MefHostExportReadResult<T>(exports, error: null);
    }

    public static MefHostExportReadResult<T> Failure(string error)
    {
        return new MefHostExportReadResult<T>(exports: [], error);
    }
}
