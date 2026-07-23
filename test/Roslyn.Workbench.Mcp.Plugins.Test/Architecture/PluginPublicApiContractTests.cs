using System.Reflection;

namespace Roslyn.Workbench.Mcp.Plugins.Test.Architecture;

public sealed class PluginPublicApiContractTests
{
    private static readonly string[] _expectedExportedTypes =
    [
        "Roslyn.Workbench.Mcp.Plugins.IMutationContext",
        "Roslyn.Workbench.Mcp.Plugins.IMutationToolHandler",
        "Roslyn.Workbench.Mcp.Plugins.IMutationToolHandler`1",
        "Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration",
        "Roslyn.Workbench.Mcp.Plugins.IQueryContext",
        "Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler",
        "Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler`2",
        "Roslyn.Workbench.Mcp.Plugins.IRoslynPlugin",
        "Roslyn.Workbench.Mcp.Plugins.IToolExecutionContext",
        "Roslyn.Workbench.Mcp.Plugins.IToolExecutionServices",
        "Roslyn.Workbench.Mcp.Plugins.MutationCandidate",
        "Roslyn.Workbench.Mcp.Plugins.MutationToolConfigurationBuilder",
        "Roslyn.Workbench.Mcp.Plugins.PluginApiVersions",
        "Roslyn.Workbench.Mcp.Plugins.PluginExecutionError",
        "Roslyn.Workbench.Mcp.Plugins.PluginExecutionOutcome",
        "Roslyn.Workbench.Mcp.Plugins.PluginExecutionOutcomeExtensions",
        "Roslyn.Workbench.Mcp.Plugins.PluginExecutionResult`1",
        "Roslyn.Workbench.Mcp.Plugins.QueryToolConfigurationBuilder",
        "Roslyn.Workbench.Mcp.Plugins.RoslynPluginAttribute",
        "Roslyn.Workbench.Mcp.Plugins.RoslynToolAttribute",
        "Roslyn.Workbench.Mcp.Plugins.Services.DependencyCycle",
        "Roslyn.Workbench.Mcp.Plugins.Services.GraphEdge",
        "Roslyn.Workbench.Mcp.Plugins.Services.GraphNode",
        "Roslyn.Workbench.Mcp.Plugins.Services.ICompilerDiagnosticService",
        "Roslyn.Workbench.Mcp.Plugins.Services.IDependencyAnalysisService",
        "Roslyn.Workbench.Mcp.Plugins.Services.IInspectionContextService",
        "Roslyn.Workbench.Mcp.Plugins.Services.IToolRequestResolver",
        "Roslyn.Workbench.Mcp.Plugins.Services.TestImpactInfo",
        "Roslyn.Workbench.Mcp.Plugins.Services.ToolResolutionResult`2",
        "Roslyn.Workbench.Mcp.Plugins.ToolConfigurationBuilder`1",
    ];

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_PluginsAssembly_WHEN_InspectingExportedTypes_THEN_ShouldMatchSupportedThirdPartySurface()
    {
        var exportedTypes = typeof(IRoslynPlugin).Assembly
            .GetExportedTypes()
            .Select(static type => type.FullName)
            .OfType<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();

        exportedTypes.Should().Equal(_expectedExportedTypes);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_PluginsPublicApi_WHEN_InspectingWorkspaceTypes_THEN_ShouldExposeOnlyContractsProjectsAndResolution()
    {
        var workspaceTypes = typeof(IRoslynPlugin).Assembly
            .GetExportedTypes()
            .SelectMany(GetPublicSignatureTypes)
            .SelectMany(GetTypeClosure)
            .Where(static type => type.Namespace?.StartsWith("Roslyn.Workbench.Mcp.Workspace", StringComparison.Ordinal) == true)
            .Distinct()
            .OrderBy(static type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        workspaceTypes.Should().OnlyContain(static type =>
            type.Namespace != null
            && (type.Namespace.StartsWith("Roslyn.Workbench.Mcp.Workspace.Contracts.", StringComparison.Ordinal)
                || string.Equals(type.Namespace, "Roslyn.Workbench.Mcp.Workspace.Projects", StringComparison.Ordinal)
                || string.Equals(type.Namespace, "Roslyn.Workbench.Mcp.Workspace.Resolution", StringComparison.Ordinal)));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_PluginExecutionContexts_WHEN_InspectingCapabilities_THEN_ShouldNotExposeHostStaging()
    {
        var mutationMembers = typeof(IMutationContext).GetMembers(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);

        var contextProperties = typeof(IToolExecutionContext).GetProperties();

        mutationMembers.Should().BeEmpty();
        contextProperties.Should().OnlyContain(static property => property.SetMethod == null);
        contextProperties.Select(static property => property.Name).Should().BeEquivalentTo(
        [
            nameof(IToolExecutionContext.CurrentSolution),
            nameof(IToolExecutionContext.DefaultMaxResults),
            nameof(IToolExecutionContext.ToolExecutionServices),
            nameof(IToolExecutionContext.TransactionRevision),
            nameof(IToolExecutionContext.WorkspaceIdentity),
            nameof(IToolExecutionContext.WorkspaceResolver),
        ]);

        typeof(IWorkspaceMutationStager).IsAssignableFrom(typeof(IMutationContext)).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_PluginExecutionResult_WHEN_InspectingConstruction_THEN_ShouldRequireOutcomeFactories()
    {
        var resultType = typeof(PluginExecutionResult<>);
        var publicConstructors = resultType.GetConstructors();
        var publicSetters = resultType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(static property => property.SetMethod)
            .OfType<MethodInfo>()
            .Where(static setter => setter.IsPublic)
            .ToArray();

        publicConstructors.Should().BeEmpty();
        publicSetters.Should().BeEmpty();
    }

    private static IEnumerable<Type> GetPublicSignatureTypes(Type type)
    {
        if (type.BaseType is not null)
        {
            yield return type.BaseType;
        }

        foreach (var implementedInterface in type.GetInterfaces())
        {
            yield return implementedInterface;
        }

        foreach (var constructor in type.GetConstructors())
        {
            foreach (var parameter in constructor.GetParameters())
            {
                yield return parameter.ParameterType;
            }
        }

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            yield return method.ReturnType;
            foreach (var parameter in method.GetParameters())
            {
                yield return parameter.ParameterType;
            }
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            yield return property.PropertyType;
        }

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            yield return field.FieldType;
        }

        foreach (var eventInfo in type.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            if (eventInfo.EventHandlerType is not null)
            {
                yield return eventInfo.EventHandlerType;
            }
        }
    }

    private static IEnumerable<Type> GetTypeClosure(Type type)
    {
        yield return type;

        if (type.HasElementType && type.GetElementType() is { } elementType)
        {
            foreach (var nestedType in GetTypeClosure(elementType))
            {
                yield return nestedType;
            }
        }

        foreach (var genericArgument in type.GetGenericArguments())
        {
            foreach (var nestedType in GetTypeClosure(genericArgument))
            {
                yield return nestedType;
            }
        }
    }
}
