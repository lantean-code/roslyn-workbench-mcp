using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

internal sealed record MefHostExportReadResult<T>
{
    public IReadOnlyList<T> Exports { get; }

    public string? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccessful => Error is null;

    internal MefHostExportReadResult(IReadOnlyList<T> exports, string? error)
    {
        Exports = exports;
        Error = error;
    }
}

internal static class MefHostExportReadResult
{
    public static MefHostExportReadResult<T> Success<T>(IReadOnlyList<T> exports)
    {
        return new MefHostExportReadResult<T>(exports, error: null);
    }

    public static MefHostExportReadResult<T> Failure<T>(string error)
    {
        return new MefHostExportReadResult<T>(exports: [], error);
    }
}
