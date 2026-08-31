using System.Reflection;

namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

/// <summary>
/// Builds the distinct assembly set used for Roslyn Code Action composition.
/// </summary>
internal static class CodeActionAssemblyResolver
{
    /// <summary>
    /// Gets Roslyn's default MEF assemblies together with the built-in feature assemblies.
    /// </summary>
    /// <returns>The distinct built-in composition assemblies.</returns>
    public static IReadOnlyList<Assembly> ResolveBuiltInAssemblies()
    {
        var assemblies = new List<Assembly>(MefHostServices.DefaultAssemblies);
        AddFeatureAssemblies(assemblies);

        return assemblies
            .Distinct(CodeActionAssemblyIdentityComparer.Instance)
            .ToArray();
    }

    /// <summary>
    /// Builds the composition assembly set from the configured built-in and additional assemblies.
    /// </summary>
    /// <param name="options">The assembly-selection settings.</param>
    /// <returns>The distinct assemblies to pass to Roslyn MEF composition.</returns>
    public static IReadOnlyList<Assembly> Resolve(CodeActionCompositionOptions options)
    {
        var assemblies = new List<Assembly>(MefHostServices.DefaultAssemblies);
        if (options.IncludeBuiltInAssemblies)
        {
            AddFeatureAssemblies(assemblies);
        }

        assemblies.AddRange(options.AdditionalAssemblies);
        return assemblies
            .Distinct(CodeActionAssemblyIdentityComparer.Instance)
            .ToArray();
    }

    private static void AddFeatureAssemblies(List<Assembly> assemblies)
    {
        assemblies.Add(Assembly.Load("Microsoft.CodeAnalysis.Features"));
        assemblies.Add(Assembly.Load("Microsoft.CodeAnalysis.CSharp.Features"));
    }
}
