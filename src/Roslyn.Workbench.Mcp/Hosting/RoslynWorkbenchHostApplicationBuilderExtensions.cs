using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.ToolExecution;

namespace Roslyn.Workbench.Mcp.Hosting;

internal static class RoslynWorkbenchHostApplicationBuilderExtensions
{
    public static IHostApplicationBuilder AddRoslynWorkbench(this IHostApplicationBuilder builder, string[] args)
    {
        var composition = HostStartupComposer.Compose(args);

        builder.Logging.ConfigureRoslynWorkbenchLogging();
        builder.Services.AddRoslynWorkbenchOptions(composition.Options);
        builder.Services.AddSingleton(composition.Configuration);
        builder.Services.AddSingleton(composition.CodeActions);
        builder.Services.AddWorkspaceServices();
        builder.Services.AddPluginServices();
        builder.Services.AddCodeActionServices();
        builder.Services.AddHostServices();
        builder.Services.AddMcpTools(
            composition.CodeActions.Tools,
            composition.Options.ErrorReporting);
        builder.Services.AddStartupPrerequisites();
        builder.Services.AddRoslynWorkbenchMcpServer();

        return builder;
    }

    private static void AddRoslynWorkbenchMcpServer(this IServiceCollection services)
    {
        services.AddSingleton<IConfigureOptions<McpServerOptions>, RoslynWorkbenchMcpServerOptionsConfiguration>();

        services
            .AddMcpServer()
            .WithRequestFilters(static requestFilters =>
            {
                requestFilters.AddCallToolFilter(static next => async (context, cancellationToken) =>
                {
                    var requestServices = context.Services
                        ?? throw new InvalidOperationException("The MCP call-tool filter requires a configured service provider.");

                    var filter = requestServices.GetRequiredService<UnhandledToolExceptionFilter>();
                    return await filter.InvokeAsync(next, context, cancellationToken);
                });
            })
            .WithStdioServerTransport();
    }
}
