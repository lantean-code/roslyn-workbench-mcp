using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Roslyn.Workbench.Mcp.Test.PluginLoading;

internal static class PluginCatalogLoaderTestFactory
{
    public const int BundledCoreToolCount = 39;

    public static PluginCatalogSnapshot Load(
        StartupOptions startupOptions,
        IReadOnlyList<System.Reflection.Assembly> bundledAssemblies,
        IEnumerable<string>? reservedToolNames = null)
    {
        var arguments = startupOptions.PluginDirectories
            .SelectMany(static directory => new[] { "--plugin-directory", directory })
            .Concat(["--tool-output-schema-mode", startupOptions.ToolOutputSchemaMode.ToString()])
            .ToArray();
        var builder = Host.CreateApplicationBuilder([]);
        builder.AddRoslynWorkbench(arguments);

        using var host = builder.Build();
        var loader = host.Services.GetRequiredService<IPluginCatalogLoader>();

        return loader.Load(startupOptions, bundledAssemblies, reservedToolNames);
    }
}
