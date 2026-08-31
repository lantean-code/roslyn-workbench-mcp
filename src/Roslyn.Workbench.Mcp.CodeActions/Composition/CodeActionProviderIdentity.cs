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

    private static string GetId(Type providerType)
    {
        return providerType.FullName ?? providerType.Name;
    }
}
