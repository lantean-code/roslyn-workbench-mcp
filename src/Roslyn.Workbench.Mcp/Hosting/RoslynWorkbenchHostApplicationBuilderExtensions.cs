using Roslyn.Workbench.Mcp.ToolExecution;

namespace Roslyn.Workbench.Mcp.Hosting;

internal static class RoslynWorkbenchHostApplicationBuilderExtensions
{
    public static IHostApplicationBuilder AddRoslynWorkbench(this IHostApplicationBuilder builder, string[] args)
    {
        var composition = new HostStartupComposer(new PluginCatalogBootstrap()).Compose(args);

        builder.Logging.ConfigureRoslynWorkbenchLogging();
        builder.Services.AddRoslynWorkbenchOptions(composition.Options);
        builder.Services.AddSingleton(composition.Configuration);
        builder.Services.AddSingleton(composition.CodeActions);
        builder.Services.AddSingleton(composition.Plugins);
        builder.Services.AddWorkspaceServices();
        builder.Services.AddPluginServices();
        builder.Services.AddCodeActionServices();
        builder.Services.AddHostServices();
        builder.Services.AddMcpTools(composition.Plugins, composition.CodeActions.Tools);
        builder.Services.AddStartupPrerequisites();
        builder.Services.AddRoslynWorkbenchMcpServer();

        return builder;
    }

    private static void AddRoslynWorkbenchMcpServer(this IServiceCollection services)
    {
        services
            .AddMcpServer()
            .WithRequestFilters(static requestFilters =>
            {
                requestFilters.AddCallToolFilter(static next => async (context, cancellationToken) =>
                {
                    var requestServices = context.Services
                        ?? throw new InvalidOperationException("The MCP call-tool filter requires a configured service provider.");
                    var filter = requestServices.GetRequiredService<UnhandledToolExceptionFilter>();
                    return await filter.InvokeAsync(next, context, cancellationToken).ConfigureAwait(false);
                });
            })
            .WithStdioServerTransport();
    }
}
