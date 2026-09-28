using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Roslyn.Workbench.Mcp.Configuration;
using Roslyn.Workbench.Mcp.Hosting;
using Roslyn.Workbench.Mcp.PluginLoading;

namespace Roslyn.Workbench.Mcp.ToolReferenceGenerator;

/// <summary>
/// Composes the production Host and returns its effective built-in MCP tool catalogue.
/// </summary>
internal static class ProductionToolCatalogueComposer
{
    private static readonly ConcurrentDictionary<(OperationalMode Mode, CommitValidationPolicy Validation, bool Reporting), Task<IReadOnlyList<Tool>>> _catalogues = new();

    private const string _pluginDirectoryEnvironmentVariable = "ROSLYN_WORKBENCH_MCP_PLUGIN_DIRECTORY";

    /// <summary>
    /// Composes tools for one effective startup policy.
    /// </summary>
    /// <param name="stateDirectory">The isolated state directory used by the composed Host.</param>
    /// <param name="operationalMode">The startup-selected operational mode.</param>
    /// <param name="commitValidation">The independent commit-validation policy.</param>
    /// <param name="reportingEnabled">Whether error-reporting tools should be published.</param>
    /// <param name="cancellationToken">The token used to cancel composition.</param>
    /// <returns>The production-composed protocol tools.</returns>
    public static async Task<List<Tool>> ComposeAsync(
        string stateDirectory,
        OperationalMode operationalMode,
        CommitValidationPolicy commitValidation,
        bool reportingEnabled,
        CancellationToken cancellationToken)
    {
        var catalogue = await _catalogues.GetOrAdd(
            (operationalMode, commitValidation, reportingEnabled),
            _ => ComposeCoreAsync(
                stateDirectory,
                operationalMode,
                commitValidation,
                reportingEnabled,
                cancellationToken));

        return new List<Tool>(catalogue);
    }

    private static async Task<IReadOnlyList<Tool>> ComposeCoreAsync(
        string stateDirectory,
        OperationalMode operationalMode,
        CommitValidationPolicy commitValidation,
        bool reportingEnabled,
        CancellationToken cancellationToken)
    {
        var previousPluginDirectory = Environment.GetEnvironmentVariable(_pluginDirectoryEnvironmentVariable);
        Environment.SetEnvironmentVariable(_pluginDirectoryEnvironmentVariable, null);

        try
        {
            var arguments = CreateArguments(
                stateDirectory,
                operationalMode,
                commitValidation,
                reportingEnabled);

            var builder = Host.CreateApplicationBuilder();
            builder.AddRoslynWorkbench(arguments);

            await using var serviceProvider = builder.Services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });

            var pluginStartup = serviceProvider.GetServices<IHostedService>()
                .OfType<PluginCatalogStartupLifecycleService>()
                .Single();

            await pluginStartup.StartingAsync(cancellationToken);

            var tools = serviceProvider.GetServices<McpServerTool>()
                .Select(static tool => tool.ProtocolTool)
                .ToList();

            var pluginCatalog = serviceProvider.GetRequiredService<IPluginCatalogState>().Current;
            tools.AddRange(pluginCatalog.Tools.Values.Select(static tool => tool.ProtocolTool));
            return tools.ToArray();
        }
        finally
        {
            Environment.SetEnvironmentVariable(_pluginDirectoryEnvironmentVariable, previousPluginDirectory);
        }
    }

    private static string[] CreateArguments(
        string stateDirectory,
        OperationalMode operationalMode,
        CommitValidationPolicy commitValidation,
        bool reportingEnabled)
    {
        return
        [
            "--operational-mode",
            OperationalModeNames.GetName(operationalMode),
            "--commit-validation",
            SecuritySurfaceNames.GetEnumName(commitValidation),
            "--error-reporting-consent",
            reportingEnabled ? "prompt" : "never",
            "--state-directory",
            stateDirectory,
            "--tool-output-schema-mode",
            "Full",
        ];
    }
}
