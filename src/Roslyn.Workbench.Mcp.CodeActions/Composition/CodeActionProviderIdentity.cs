namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

/// <summary>
/// Derives stable catalogue identifiers from Code Action provider types.
/// </summary>
internal static class CodeActionProviderIdentity
{
    /// <summary>
    /// Gets the stable identity of a Code Fix provider.
    /// </summary>
    /// <param name="provider">The Code Fix provider to identify.</param>
    /// <returns>The provider type's fully qualified name, or its simple name when no qualified name is available.</returns>
    public static string GetId(CodeFixProvider provider)
    {
        return GetId(provider.GetType());
    }

    /// <summary>
    /// Gets the stable identity of a refactoring provider.
    /// </summary>
    /// <param name="provider">The refactoring provider to identify.</param>
    /// <returns>The provider type's fully qualified name, or its simple name when no qualified name is available.</returns>
    public static string GetId(CodeRefactoringProvider provider)
    {
        return GetId(provider.GetType());
    }

    /// <summary>
    /// Creates concise runtime provenance for a provider type.
    /// </summary>
    /// <param name="providerType">The provider runtime type to identify.</param>
    /// <returns>The provider type and assembly identity used for diagnostics and review.</returns>
    public static MutationProviderIdentity CreateProvenance(Type providerType)
    {
        var assemblyName = providerType.Assembly.GetName();
        var simpleAssemblyName = assemblyName.Name
            ?? throw new InvalidOperationException(
                $"Code Action provider type '{GetId(providerType)}' has no assembly simple name.");

        var assemblyVersion = assemblyName.Version?.ToString()
            ?? throw new InvalidOperationException(
                $"Code Action provider type '{GetId(providerType)}' has no assembly version.");

        return new MutationProviderIdentity
        {
            TypeName = GetId(providerType),
            AssemblyName = simpleAssemblyName,
            AssemblyVersion = assemblyVersion,
        };
    }

    private static string GetId(Type providerType)
    {
        return providerType.FullName ?? providerType.Name;
    }
}
