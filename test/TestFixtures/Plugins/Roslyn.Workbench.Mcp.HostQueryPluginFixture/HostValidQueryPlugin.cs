using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using NuGet.Versioning;
using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Workspace.Selectors;

namespace Roslyn.Workbench.Mcp.HostQueryPluginFixture;

[RoslynPlugin("host.valid.query", "Host Valid Query Plugin", PluginApiVersions.V1)]
public sealed class HostValidQueryPlugin : IRoslynPlugin
{
    public void Configure(IPluginConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        configuration.Services.AddSingleton<IPrivateDependencyVersionProvider, PrivateDependencyVersionProvider>();
        configuration.AddQueryTool<Handler>();
        configuration.AddQueryTool<QueryCacheCalibrationHandler>();
    }

    public sealed record QueryCacheCalibrationRequest : WorkspaceBoundRequest
    {
        public string Workload { get; init; } = string.Empty;

        public int KeyCount { get; init; } = 1;

        public int PayloadLength { get; init; } = 1024;

        public int FactoryDelayMilliseconds { get; init; }

        public bool UseSynchronousFactory { get; init; }

        public bool ReturnNull { get; init; }

        public bool ReturnDisposable { get; init; }

        public bool IncludeFactoryExecutionCount { get; init; }
    }

    public sealed record QueryCacheCalibrationResponse
    {
        public required string Workload { get; init; }

        public required int KeyIndex { get; init; }

        public required int FactoryExecutionCount { get; init; }

        public required int PayloadLength { get; init; }
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
        private readonly IPrivateDependencyVersionProvider _privateDependencyVersionProvider;

        public Handler(IPrivateDependencyVersionProvider privateDependencyVersionProvider)
        {
            _privateDependencyVersionProvider = privateDependencyVersionProvider;
        }

        public async ValueTask<PluginExecutionResult<Response>> ExecuteAsync(Request request, IQueryContext context, CancellationToken cancellationToken)
        {
            await PluginFixtureControl.WaitForReleaseAsync(request.ControlDirectory, cancellationToken);
            if (request.Throw)
            {
                throw new InvalidOperationException("Sensitive query failure.");
            }

            var response = new Response
            {
                Value = request.Name,
                PrivateDependencyVersion = _privateDependencyVersionProvider.GetVersion(),
            };

            var result = PluginExecutionResult.Success(response);
            return result;
        }
    }

    private interface IPrivateDependencyVersionProvider
    {
        string GetVersion();
    }

    private sealed class PrivateDependencyVersionProvider : IPrivateDependencyVersionProvider
    {
        public PrivateDependencyVersionProvider()
        {
        }

        public string GetVersion()
        {
            return typeof(NuGetVersion).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? string.Empty;
        }
    }

    [RoslynTool(
        "host-query-cache-calibration",
        "Host Query Cache Calibration",
        "Exercises plugin query-cache reuse, pressure, coalescing and non-admission.")]
    private sealed class QueryCacheCalibrationHandler :
        IQueryToolHandler<QueryCacheCalibrationRequest, QueryCacheCalibrationResponse>
    {
        private static readonly ConcurrentDictionary<string, int> _factoryExecutions =
            new(StringComparer.Ordinal);

        private static readonly ConcurrentDictionary<string, int> _invocations =
            new(StringComparer.Ordinal);

        public async ValueTask<PluginExecutionResult<QueryCacheCalibrationResponse>> ExecuteAsync(
            QueryCacheCalibrationRequest request,
            IQueryContext context,
            CancellationToken cancellationToken)
        {
            var keyCount = Math.Max(1, request.KeyCount);
            var invocation = _invocations.AddOrUpdate(
                request.Workload,
                static _ => 0,
                static (_, current) => checked(current + 1));
            var keyIndex = invocation % keyCount;
            var key = new QueryCacheCalibrationKey(request.Workload, keyIndex);
            CachePayload? payload;

            if (request.ReturnDisposable)
            {
#pragma warning disable RWMCP021 // The fixture deliberately proves runtime non-admission of disposable values.
                var disposable = await context.QueryResultCache.GetOrCreateAsync(
                    key,
                    token => CreateDisposableAsync(request, key, token),
                    cancellationToken);
#pragma warning restore RWMCP021
                payload = disposable?.Payload;
                disposable?.Dispose();
            }
            else if (request.UseSynchronousFactory)
            {
                payload = context.QueryResultCache.GetOrCreate(
                    key,
                    token => CreatePayload(request, key, token),
                    cancellationToken);
            }
            else
            {
                payload = await context.QueryResultCache.GetOrCreateAsync(
                    key,
                    token => CreatePayloadAsync(request, key, token),
                    cancellationToken);
            }

            var response = new QueryCacheCalibrationResponse
            {
                Workload = request.Workload,
                KeyIndex = keyIndex,
                FactoryExecutionCount = request.IncludeFactoryExecutionCount
                    ? _factoryExecutions.GetValueOrDefault(GetExecutionKey(key))
                    : 0,
                PayloadLength = payload?.Value.Length ?? 0,
            };

            return PluginExecutionResult.Success(response);
        }

        private static CachePayload? CreatePayload(
            QueryCacheCalibrationRequest request,
            QueryCacheCalibrationKey key,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RecordFactoryExecution(key);
            if (request.ReturnNull)
            {
                return null;
            }

            var payloadLength = Math.Max(0, request.PayloadLength);
            var value = new string('x', payloadLength);
            var payload = new CachePayload(value);
            return payload;
        }

        private static async ValueTask<CachePayload?> CreatePayloadAsync(
            QueryCacheCalibrationRequest request,
            QueryCacheCalibrationKey key,
            CancellationToken cancellationToken)
        {
            if (request.FactoryDelayMilliseconds > 0)
            {
                await Task.Delay(request.FactoryDelayMilliseconds, cancellationToken);
            }

            return CreatePayload(request, key, cancellationToken);
        }

        private static async ValueTask<DisposableCachePayload?> CreateDisposableAsync(
            QueryCacheCalibrationRequest request,
            QueryCacheCalibrationKey key,
            CancellationToken cancellationToken)
        {
            var payload = await CreatePayloadAsync(request, key, cancellationToken);
            if (payload is null)
            {
                return null;
            }

            var disposablePayload = new DisposableCachePayload(payload);
            return disposablePayload;
        }

        private static void RecordFactoryExecution(QueryCacheCalibrationKey key)
        {
            _factoryExecutions.AddOrUpdate(
                GetExecutionKey(key),
                static _ => 1,
                static (_, current) => checked(current + 1));
        }

        private static string GetExecutionKey(QueryCacheCalibrationKey key)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{key.Workload}:{key.KeyIndex}");
        }

        private sealed record QueryCacheCalibrationKey(
            string Workload,
            int KeyIndex) : IQueryResultCacheKey;

        private sealed record CachePayload(string Value);

        private sealed class DisposableCachePayload : IDisposable
        {
            public CachePayload Payload { get; }

            public DisposableCachePayload(CachePayload payload)
            {
                Payload = payload;
            }

            public void Dispose()
            {
            }
        }
    }
}
