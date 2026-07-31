namespace Roslyn.Workbench.Mcp.Plugins.Analyzers.Test;

public sealed class PluginQueryCacheAnalyzerTests
{
    [Fact]
    public async Task GIVEN_MutableKey_WHEN_UsingQueryCache_THEN_ShouldReportRwmcp020()
    {
        const string source = """
            public sealed class Key : Roslyn.Workbench.Mcp.Plugins.IQueryResultCacheKey
            {
                public string Value { get; set; }
            }

            public static class Consumer
            {
                public static string Get(
                    Roslyn.Workbench.Mcp.Plugins.IQueryResultCache cache)
                {
                    return cache.GetOrCreate({|RWMCP020:new Key()|}, _ => "Value", default);
                }
            }
            """;

        await AnalyzerVerifier.VerifyQueryCacheAsync(source);
    }

    [Fact]
    public async Task GIVEN_ImmutableRecordKey_WHEN_UsingQueryCache_THEN_ShouldNotReportKeyDiagnostic()
    {
        const string source = """
            public sealed record Key : Roslyn.Workbench.Mcp.Plugins.IQueryResultCacheKey
            {
                public string Value { get; init; }
            }

            public static class Consumer
            {
                public static string Get(
                    Roslyn.Workbench.Mcp.Plugins.IQueryResultCache cache)
                {
                    return cache.GetOrCreate(new Key(), _ => "Value", default);
                }
            }
            """;

        await AnalyzerVerifier.VerifyQueryCacheAsync(source);
    }

    [Fact]
    public async Task GIVEN_DisposableValue_WHEN_UsingQueryCache_THEN_ShouldReportRwmcp021()
    {
        const string source = """
            public sealed record Key : Roslyn.Workbench.Mcp.Plugins.IQueryResultCacheKey;

            public static class Consumer
            {
                public static System.IO.MemoryStream Get(
                    Roslyn.Workbench.Mcp.Plugins.IQueryResultCache cache)
                {
                    return {|RWMCP021:cache.GetOrCreate(new Key(), _ => new System.IO.MemoryStream(), default)|};
                }
            }
            """;

        await AnalyzerVerifier.VerifyQueryCacheAsync(source);
    }

    [Fact]
    public async Task GIVEN_PluginResultEnvelope_WHEN_UsingQueryCache_THEN_ShouldReportRwmcp021()
    {
        const string source = """
            public sealed record Key : Roslyn.Workbench.Mcp.Plugins.IQueryResultCacheKey;

            public static class Consumer
            {
                public static Roslyn.Workbench.Mcp.Plugins.PluginExecutionResult<string> Get(
                    Roslyn.Workbench.Mcp.Plugins.IQueryResultCache cache)
                {
                    return {|RWMCP021:cache.GetOrCreate(
                        new Key(),
                        _ => new Roslyn.Workbench.Mcp.Plugins.PluginExecutionResult<string>(),
                        default)|};
                }
            }
            """;

        await AnalyzerVerifier.VerifyQueryCacheAsync(source);
    }

    [Fact]
    public async Task GIVEN_KeyContainsUnsafeStruct_WHEN_UsingQueryCache_THEN_ShouldReportRwmcp020()
    {
        const string source = """
            public readonly struct UnsafePart
            {
                public System.IO.MemoryStream Stream { get; }

                public UnsafePart(System.IO.MemoryStream stream)
                {
                    Stream = stream;
                }
            }

            public sealed record Key : Roslyn.Workbench.Mcp.Plugins.IQueryResultCacheKey
            {
                public UnsafePart Part { get; init; }
            }

            public static class Consumer
            {
                public static string Get(
                    Roslyn.Workbench.Mcp.Plugins.IQueryResultCache cache)
                {
                    return cache.GetOrCreate({|RWMCP020:new Key()|}, _ => "Value", default);
                }
            }
            """;

        await AnalyzerVerifier.VerifyQueryCacheAsync(source);
    }

    [Fact]
    public async Task GIVEN_ValueContainsUnsafeStruct_WHEN_UsingQueryCache_THEN_ShouldReportRwmcp021()
    {
        const string source = """
            public sealed record Key : Roslyn.Workbench.Mcp.Plugins.IQueryResultCacheKey;

            public readonly struct UnsafeValue
            {
                public System.IO.MemoryStream Stream { get; }

                public UnsafeValue(System.IO.MemoryStream stream)
                {
                    Stream = stream;
                }
            }

            public static class Consumer
            {
                public static UnsafeValue Get(
                    Roslyn.Workbench.Mcp.Plugins.IQueryResultCache cache)
                {
                    return {|RWMCP021:cache.GetOrCreate(
                        new Key(),
                        _ => new UnsafeValue(new System.IO.MemoryStream()),
                        default)|};
                }
            }
            """;

        await AnalyzerVerifier.VerifyQueryCacheAsync(source);
    }

    [Fact]
    public async Task GIVEN_KeyInheritsMutableState_WHEN_UsingQueryCache_THEN_ShouldReportRwmcp020()
    {
        const string source = """
            public class MutableBase
            {
                public string Value { get; set; }
            }

            public sealed class Key :
                MutableBase,
                Roslyn.Workbench.Mcp.Plugins.IQueryResultCacheKey
            {
                public override bool Equals(object other)
                {
                    return other is Key;
                }

                public override int GetHashCode()
                {
                    return 0;
                }
            }

            public static class Consumer
            {
                public static string Get(
                    Roslyn.Workbench.Mcp.Plugins.IQueryResultCache cache)
                {
                    return cache.GetOrCreate({|RWMCP020:new Key()|}, _ => "Value", default);
                }
            }
            """;

        await AnalyzerVerifier.VerifyQueryCacheAsync(source);
    }

    [Fact]
    public async Task GIVEN_ValueInheritsMutableState_WHEN_UsingQueryCache_THEN_ShouldReportRwmcp021()
    {
        const string source = """
            public sealed record Key : Roslyn.Workbench.Mcp.Plugins.IQueryResultCacheKey;

            public class MutableBase
            {
                public string Value { get; set; }
            }

            public sealed class CachedValue : MutableBase
            {
            }

            public static class Consumer
            {
                public static CachedValue Get(
                    Roslyn.Workbench.Mcp.Plugins.IQueryResultCache cache)
                {
                    return {|RWMCP021:cache.GetOrCreate(
                        new Key(),
                        _ => new CachedValue(),
                        default)|};
                }
            }
            """;

        await AnalyzerVerifier.VerifyQueryCacheAsync(source);
    }

    [Fact]
    public async Task GIVEN_ImmutableArrayShapes_WHEN_UsingQueryCache_THEN_ShouldNotReportDiagnostic()
    {
        const string source = """
            public sealed record Key : Roslyn.Workbench.Mcp.Plugins.IQueryResultCacheKey
            {
                public System.Collections.Immutable.ImmutableArray<string> Values { get; init; }
                    = System.Collections.Immutable.ImmutableArray<string>.Empty;
            }

            public static class Consumer
            {
                public static System.Collections.Immutable.ImmutableArray<string> Get(
                    Roslyn.Workbench.Mcp.Plugins.IQueryResultCache cache)
                {
                    return cache.GetOrCreate(
                        new Key(),
                        _ => System.Collections.Immutable.ImmutableArray<string>.Empty,
                        default);
                }
            }
            """;

        await AnalyzerVerifier.VerifyQueryCacheAsync(source);
    }
}
