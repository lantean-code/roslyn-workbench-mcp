using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

/// <summary>
/// Represents either activated Roslyn MEF exports or a compatibility failure.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
internal sealed record MefHostExportReadResult<T>
{
    /// <summary>
    /// Gets the activated exports; empty when the read failed.
    /// </summary>
    public IReadOnlyList<T> Exports { get; }

    /// <summary>
    /// Gets the failure explanation, or <see langword="null"/> when the read succeeded.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// Gets a value indicating whether the operation completed successfully.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccessful => Error is null;

    /// <summary>
    /// Initializes a new instance of the <see cref="MefHostExportReadResult{T}"/> class.
    /// </summary>
    /// <param name="exports">The MEF exports returned by the successful lookup.</param>
    /// <param name="error">The error that caused the operation to fail.</param>
    internal MefHostExportReadResult(IReadOnlyList<T> exports, string? error)
    {
        Exports = exports;
        Error = error;
    }
}

/// <summary>
/// Creates success and failure results for Roslyn MEF export reads.
/// </summary>
internal static class MefHostExportReadResult
{
    /// <summary>
    /// Creates a result that represents successful completion.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="exports">The MEF exports returned by the successful lookup.</param>
    /// <returns>A result that represents successful completion.</returns>
    public static MefHostExportReadResult<T> Success<T>(IReadOnlyList<T> exports)
    {
        return new MefHostExportReadResult<T>(exports, error: null);
    }

    /// <summary>
    /// Creates a result that represents failure.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="error">The error that caused the operation to fail.</param>
    /// <returns>A result that represents failure.</returns>
    public static MefHostExportReadResult<T> Failure<T>(string error)
    {
        return new MefHostExportReadResult<T>(exports: [], error);
    }
}
