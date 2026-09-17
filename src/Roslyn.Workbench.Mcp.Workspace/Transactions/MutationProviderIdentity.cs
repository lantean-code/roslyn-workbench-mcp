namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Identifies the runtime type and assembly that supplied a mutation.
/// </summary>
internal sealed record MutationProviderIdentity
{
    /// <summary>
    /// Gets the fully qualified runtime provider type name.
    /// </summary>
    [Description("Fully qualified runtime type of the provider that supplied the mutation.")]
    public required string TypeName { get; init; }

    /// <summary>
    /// Gets the simple name of the provider assembly.
    /// </summary>
    [Description("Simple name of the assembly containing the provider.")]
    public required string AssemblyName { get; init; }

    /// <summary>
    /// Gets the provider assembly version.
    /// </summary>
    [Description("Version of the assembly containing the provider.")]
    public required string AssemblyVersion { get; init; }
}
