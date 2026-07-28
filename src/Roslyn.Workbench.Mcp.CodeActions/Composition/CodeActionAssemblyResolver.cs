using System.Reflection;

namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

internal static class CodeActionAssemblyResolver
{
    public static IReadOnlyList<Assembly> ResolveBuiltInAssemblies()
    {
        var assemblies = new List<Assembly>(MefHostServices.DefaultAssemblies);
        AddFeatureAssemblies(assemblies);

        return assemblies
            .Distinct(CodeActionAssemblyIdentityComparer.Instance)
            .ToArray();
    }

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
