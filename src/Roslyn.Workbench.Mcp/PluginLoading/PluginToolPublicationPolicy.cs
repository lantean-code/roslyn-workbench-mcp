namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Decides whether a plugin tool kind may be published by the effective source-mutation policy.
/// </summary>
internal static class PluginToolPublicationPolicy
{
    /// <summary>
    /// Determines whether a plugin tool may be materialized for publication.
    /// </summary>
    /// <param name="kind">The plugin tool kind.</param>
    /// <param name="sourceMutationEnabled">Whether the effective Host policy permits source mutation.</param>
    /// <returns><see langword="true"/> when the tool may be published; otherwise, <see langword="false"/>.</returns>
    public static bool ShouldPublish(ToolKind kind, bool sourceMutationEnabled)
    {
        return sourceMutationEnabled || kind == ToolKind.Query;
    }
}
