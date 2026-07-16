using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.PluginLoading;

internal sealed record PluginCompositionResult
{
    [MemberNotNullWhen(false, nameof(Error))]
    public bool Succeeded => Error is null;

    public string? Error { get; }

    private PluginCompositionResult(string? error)
    {
        Error = error;
    }

    public static PluginCompositionResult Success()
    {
        return new PluginCompositionResult(error: null);
    }

    public static PluginCompositionResult Failure(string error)
    {
        return new PluginCompositionResult(error);
    }
}
