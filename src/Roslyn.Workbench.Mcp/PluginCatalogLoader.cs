using System.Reflection;
using System.Runtime.Loader;
using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp;

internal sealed class PluginCatalogLoader
{
    public PluginCatalogSnapshot Load(
        StartupOptions startupOptions,
        IReadOnlyList<Assembly> bundledAssemblies,
        IEnumerable<string>? reservedToolNames = null)
    {
        var tools = new List<IRegisteredPluginTool>();
        var pluginStatuses = new List<PluginStatus>();
        var toolNames = new HashSet<string>(reservedToolNames ?? [], StringComparer.Ordinal);

        foreach (var assembly in ResolveAssemblies(startupOptions, bundledAssemblies))
        {
            var loadResult = TryLoadAssembly(assembly, toolNames);
            pluginStatuses.Add(loadResult.Status);

            if (loadResult.Status.Enabled)
            {
                foreach (var tool in loadResult.Tools)
                {
                    tools.Add(tool);
                    toolNames.Add(tool.Tool.Metadata.Name);
                }
            }
        }

        return new PluginCatalogSnapshot
        {
            Tools = tools,
            Plugins = pluginStatuses,
        };
    }

    private static IReadOnlyList<Assembly> ResolveAssemblies(StartupOptions startupOptions, IReadOnlyList<Assembly> bundledAssemblies)
    {
        var assemblies = new List<Assembly>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var assembly in bundledAssemblies)
        {
            if (!string.IsNullOrWhiteSpace(assembly.Location) && seen.Add(assembly.Location))
            {
                assemblies.Add(assembly);
            }
        }

        foreach (var directory in startupOptions.PluginDirectories)
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(directory, "*.dll"))
            {
                if (!seen.Add(path))
                {
                    continue;
                }

                var assemblyName = AssemblyName.GetAssemblyName(path);
                var existingAssembly = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .FirstOrDefault(loadedAssembly => string.Equals(loadedAssembly.GetName().FullName, assemblyName.FullName, StringComparison.Ordinal));

                assemblies.Add(existingAssembly ?? AssemblyLoadContext.Default.LoadFromAssemblyPath(path));
            }
        }

        return assemblies;
    }

    private static PluginAssemblyLoadResult TryLoadAssembly(Assembly assembly, ISet<string> globalToolNames)
    {
        try
        {
            var pluginTypes = assembly
                .GetTypes()
                .Where(static type => typeof(IRoslynPlugin).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
                .ToArray();

            if (pluginTypes.Length != 1)
            {
                return DisabledAssembly(
                    assembly,
                    assembly.GetName().Name ?? assembly.FullName ?? "unknown-assembly",
                    assembly.GetName().Name ?? assembly.FullName ?? "unknown-assembly",
                    assembly.GetName().Version?.ToString() ?? "0.0.0",
                    pluginTypes.Length == 0
                        ? "Plugin assembly does not contain an IRoslynPlugin entry point."
                        : "Plugin assembly contains multiple IRoslynPlugin entry points.");
            }

            if (Activator.CreateInstance(pluginTypes[0]) is not IRoslynPlugin plugin)
            {
                return DisabledAssembly(
                    assembly,
                    assembly.GetName().Name ?? assembly.FullName ?? "unknown-assembly",
                    assembly.GetName().Name ?? assembly.FullName ?? "unknown-assembly",
                    assembly.GetName().Version?.ToString() ?? "0.0.0",
                    "Plugin entry point could not be created.");
            }
            var registry = new PluginRegistry(plugin.Metadata);
            plugin.Register(registry);

            if (registry.RegisteredPluginTools.Any(tool => globalToolNames.Contains(tool.Tool.Metadata.Name)))
            {
                return DisabledPlugin(plugin.Metadata, "Plugin tool names must be globally unique across loaded plugins.");
            }

            var diagnostics = registry.RegisteredPluginTools
                .SelectMany(static tool => QueryResponseContractInspector.Inspect(tool.Tool))
                .ToArray();

            return new PluginAssemblyLoadResult(
                new PluginStatus
                {
                    PluginId = plugin.Metadata.PluginId,
                    DisplayName = plugin.Metadata.DisplayName,
                    Version = plugin.Metadata.Version,
                    SupportedApiVersion = plugin.Metadata.SupportedApiVersion,
                    Enabled = true,
                    Diagnostics = diagnostics,
                },
                registry.RegisteredPluginTools);
        }
        catch (Exception exception)
        {
            var pluginId = assembly.GetName().Name ?? assembly.FullName ?? "unknown-assembly";
            var version = assembly.GetName().Version?.ToString() ?? "0.0.0";

            return DisabledAssembly(assembly, pluginId, pluginId, version, exception.Message);
        }
    }

    private static PluginAssemblyLoadResult DisabledAssembly(
        Assembly assembly,
        string pluginId,
        string displayName,
        string version,
        string message)
    {
        return new PluginAssemblyLoadResult(
            new PluginStatus
            {
                PluginId = pluginId,
                DisplayName = displayName,
                Version = version,
                SupportedApiVersion = PluginApiVersions.V1,
                Enabled = false,
                Diagnostics =
                [
                    CreateDiagnostic(assembly, message),
                ],
            },
            []);
    }

    private static PluginAssemblyLoadResult DisabledPlugin(PluginMetadata metadata, string message)
    {
        return new PluginAssemblyLoadResult(
            new PluginStatus
            {
                PluginId = metadata.PluginId,
                DisplayName = metadata.DisplayName,
                Version = metadata.Version,
                SupportedApiVersion = metadata.SupportedApiVersion,
                Enabled = false,
                Diagnostics =
                [
                    new DiagnosticInfo
                    {
                        Id = "PluginLoad",
                        Severity = DiagnosticSeverity.Error,
                        Message = message,
                    },
                ],
            },
            []);
    }

    private static DiagnosticInfo CreateDiagnostic(Assembly assembly, string message)
    {
        return new DiagnosticInfo
        {
            Id = "PluginLoad",
            Severity = DiagnosticSeverity.Error,
            Message = $"{assembly.GetName().Name}: {message}",
        };
    }

    private sealed record PluginAssemblyLoadResult(PluginStatus Status, IReadOnlyList<IRegisteredPluginTool> Tools);
}
