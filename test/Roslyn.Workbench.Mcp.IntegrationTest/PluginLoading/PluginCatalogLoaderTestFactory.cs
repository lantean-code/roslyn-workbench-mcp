using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Roslyn.Workbench.Mcp.Test.PluginLoading;

internal static class PluginCatalogLoaderTestFactory
{
    public const int BundledCoreToolCount = 39;
    public const int BundledCoreQueryToolCount = 37;

    public static PluginCatalogSnapshot Load(
        StartupOptions startupOptions,
        IReadOnlyList<System.Reflection.Assembly> bundledAssemblies,
        IEnumerable<string>? reservedToolNames = null)
    {
        var arguments = startupOptions.PluginDirectories
            .SelectMany(static directory => new[] { "--plugin-directory", directory })
            .ToList();

        if (startupOptions.ExternalPluginsEnabled)
        {
            arguments.Add("--enable-plugins");
        }

        arguments.AddRange(["--operational-mode", GetOperationalModeName(startupOptions.OperationalMode)]);
        arguments.AddRange(["--tool-output-schema-mode", startupOptions.ToolOutputSchemaMode.ToString()]);

        var builder = Host.CreateApplicationBuilder([]);
        builder.AddRoslynWorkbench(arguments.ToArray());

        using var host = builder.Build();
        var loader = host.Services.GetRequiredService<IPluginCatalogLoader>();

        return loader.Load(startupOptions, bundledAssemblies, reservedToolNames);
    }

    private static string GetOperationalModeName(OperationalMode mode)
    {
        return mode switch
        {
            OperationalMode.InspectionOnly => "inspection-only",
            OperationalMode.Transactional => "transactional",
            OperationalMode.AutonomousTrusted => "autonomous-trusted",
            _ => throw new InvalidOperationException("The operational mode is unavailable in this test fixture."),
        };
    }
}
