namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

internal sealed record CodeActionCompositionStatus
{
    public bool IsAvailable { get; }

    public string? Version { get; }

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

    public static CodeActionCompositionStatus Available(
        string? version = null,
        string? message = null)
    {
        return new CodeActionCompositionStatus(
            isAvailable: true,
            version,
            message);
    }

    public static CodeActionCompositionStatus Unavailable(string message)
    {
        return new CodeActionCompositionStatus(
            isAvailable: false,
            version: null,
            message);
    }
}
