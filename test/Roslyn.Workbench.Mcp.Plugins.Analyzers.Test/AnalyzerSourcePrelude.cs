namespace Roslyn.Workbench.Mcp.Plugins.Analyzers.Test;

internal static class AnalyzerSourcePrelude
{
    public const string Code = """
        namespace Microsoft.CodeAnalysis
        {
            public sealed class Solution
            {
                public Workspace Workspace { get; } = null;
            }

            public abstract class Workspace
            {
                public Solution CurrentSolution { get; } = null;

                public bool TryApplyChanges(Solution solution)
                {
                    return true;
                }
            }
        }

        namespace Roslyn.Workbench.Mcp.Plugins
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class RoslynPluginAttribute : System.Attribute
            {
                public RoslynPluginAttribute(string pluginId, string displayName, string apiVersion)
                {
                }
            }

            public interface IRoslynPlugin
            {
                void Configure(IPluginConfiguration configuration);
            }

            public interface IPluginConfiguration
            {
                QueryToolConfigurationBuilder AddQueryTool<THandler>();
            }

            public interface IQueryToolHandler
            {
            }

            public interface IMutationToolHandler
            {
            }

            public abstract record WorkspaceBoundRequest;

            public interface IQueryToolHandler<TRequest, TResponse> : IQueryToolHandler
                where TRequest : WorkspaceBoundRequest
            {
            }

            public interface IMutationToolHandler<TRequest> : IMutationToolHandler
                where TRequest : WorkspaceBoundRequest
            {
            }

            public sealed class BoundedCollection<TItem>
            {
            }

            public static class PluginApiVersions
            {
                public const string V1 = "1.0";
            }

            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class RoslynToolAttribute : System.Attribute
            {
                public bool Destructive { get; set; }

                public RoslynToolAttribute(string name, string title, string description)
                {
                }
            }

            public abstract class ToolConfigurationBuilder<TBuilder>
                where TBuilder : ToolConfigurationBuilder<TBuilder>
            {
            }

            public sealed class QueryToolConfigurationBuilder :
                ToolConfigurationBuilder<QueryToolConfigurationBuilder>
            {
            }

            public sealed class MutationToolConfigurationBuilder :
                ToolConfigurationBuilder<MutationToolConfigurationBuilder>
            {
            }
        }

        namespace System.Composition
        {
            [System.AttributeUsage(
                System.AttributeTargets.Constructor
                | System.AttributeTargets.Field
                | System.AttributeTargets.Parameter
                | System.AttributeTargets.Property)]
            public sealed class ImportAttribute : System.Attribute
            {
            }

            [System.AttributeUsage(
                System.AttributeTargets.Field
                | System.AttributeTargets.Parameter
                | System.AttributeTargets.Property)]
            public sealed class ImportManyAttribute : System.Attribute
            {
            }

            [System.AttributeUsage(System.AttributeTargets.Constructor)]
            public sealed class ImportingConstructorAttribute : System.Attribute
            {
            }
        }

        """;
}
