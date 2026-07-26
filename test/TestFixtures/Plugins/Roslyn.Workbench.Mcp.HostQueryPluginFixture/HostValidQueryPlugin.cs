using System.Reflection;
using NuGet.Versioning;
using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Workspace.Selectors;

namespace Roslyn.Workbench.Mcp.TestSupport;

[RoslynPlugin("host.valid.query", "Host Valid Query Plugin", PluginApiVersions.V1)]
public sealed class HostValidQueryPlugin : IRoslynPlugin
{
    public void Configure(IPluginConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _ = configuration.AddQueryTool<Handler>();
    }

    public sealed record Request : WorkspaceBoundRequest
    {
        public string Name { get; init; } = string.Empty;

        public string? ControlDirectory { get; init; }

        public bool Throw { get; init; }
    }

    public sealed record Response
    {
        public string Value { get; init; } = string.Empty;

        public string PrivateDependencyVersion { get; init; } = string.Empty;
    }

    [RoslynTool("host-valid-query", "Host Valid Query", "Returns a stable host test payload.")]
    private sealed class Handler : IQueryToolHandler<Request, Response>
    {
        public async ValueTask<PluginExecutionResult<Response>> ExecuteAsync(Request request, IQueryContext context, CancellationToken cancellationToken)
        {
            _ = context;

            await PluginFixtureControl.WaitForReleaseAsync(request.ControlDirectory, cancellationToken);
            if (request.Throw)
            {
                throw new InvalidOperationException("Sensitive query failure.");
            }

            var response = new Response
            {
                Value = request.Name,
                PrivateDependencyVersion = typeof(NuGetVersion).Assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                    ?? string.Empty,
            };

            var result = PluginExecutionResult.Success(response);
            return result;
        }
    }
}
