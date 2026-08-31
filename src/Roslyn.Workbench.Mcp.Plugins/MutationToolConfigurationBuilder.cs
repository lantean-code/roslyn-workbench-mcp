namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Supplies fluent metadata overrides for one mutation tool.
/// </summary>
public sealed class MutationToolConfigurationBuilder : ToolConfigurationBuilder<MutationToolConfigurationBuilder>
{
    private bool? _destructive;

    /// <inheritdoc cref="IToolConfigurationBuilderState.Destructive"/>
    internal override bool? Destructive => _destructive;

    /// <summary>
    /// Overrides whether the tool can replace, remove, or persist source.
    /// </summary>
    /// <param name="destructive">Whether the tool is destructive.</param>
    /// <returns>The same builder.</returns>
    public MutationToolConfigurationBuilder IsDestructive(bool destructive = true)
    {
        EnsureMutable();
        _destructive = destructive;
        return this;
    }
}
