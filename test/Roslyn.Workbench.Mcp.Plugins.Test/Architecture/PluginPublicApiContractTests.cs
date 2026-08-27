using System.Reflection;

using Roslyn.Workbench.Mcp.Workspace.Hierarchy;
using Roslyn.Workbench.Mcp.Workspace.References;

namespace Roslyn.Workbench.Mcp.Plugins.Test.Architecture;

public sealed class PluginPublicApiContractTests
{
    private static readonly string[] _expectedAbstractionsExportedTypes =
    [
        "Roslyn.Workbench.Mcp.Workspace.Hierarchy.ITypeHierarchyService",
        "Roslyn.Workbench.Mcp.Workspace.Hierarchy.TypeHierarchyMatch",
        "Roslyn.Workbench.Mcp.Workspace.Paths.IWorkspacePathService",
        "Roslyn.Workbench.Mcp.Workspace.Projects.IProjectStructureService",
        "Roslyn.Workbench.Mcp.Workspace.Projects.IProjectTargetFrameworkResolver",
        "Roslyn.Workbench.Mcp.Workspace.Projects.ProjectTargetFrameworksResult",
        "Roslyn.Workbench.Mcp.Workspace.Projects.SolutionFolderInfo",
        "Roslyn.Workbench.Mcp.Workspace.Projects.SolutionHierarchyResult",
        "Roslyn.Workbench.Mcp.Workspace.References.IReferenceDiscoveryService",
        "Roslyn.Workbench.Mcp.Workspace.References.ReferenceOccurrence",
        "Roslyn.Workbench.Mcp.Workspace.Resolution.IWorkspaceResolver",
        "Roslyn.Workbench.Mcp.Workspace.Resolution.SelectorResolveResult",
        "Roslyn.Workbench.Mcp.Workspace.Resolution.SelectorResolveResult`1",
        "Roslyn.Workbench.Mcp.Workspace.Resolution.SelectorResolveStatus",
        "Roslyn.Workbench.Mcp.Workspace.Resolution.SnapshotMatchKind",
        "Roslyn.Workbench.Mcp.Workspace.Resolution.SnapshotMatchResult",
        "Roslyn.Workbench.Mcp.Workspace.Results.BoundedCollection",
        "Roslyn.Workbench.Mcp.Workspace.Results.BoundedCollection`1",
        "Roslyn.Workbench.Mcp.Workspace.Results.ChangeSummary",
        "Roslyn.Workbench.Mcp.Workspace.Results.DiagnosticInfo",
        "Roslyn.Workbench.Mcp.Workspace.Results.DiagnosticSeverity",
        "Roslyn.Workbench.Mcp.Workspace.Results.DiffSummary",
        "Roslyn.Workbench.Mcp.Workspace.Results.DocumentChange",
        "Roslyn.Workbench.Mcp.Workspace.Results.DocumentChangeKind",
        "Roslyn.Workbench.Mcp.Workspace.Results.RequiredAction",
        "Roslyn.Workbench.Mcp.Workspace.Results.ResultLimit",
        "Roslyn.Workbench.Mcp.Workspace.Results.WarningInfo",
        "Roslyn.Workbench.Mcp.Workspace.Results.WorkspaceIdentity",
        "Roslyn.Workbench.Mcp.Workspace.Selectors.CanonicalLocationSelector",
        "Roslyn.Workbench.Mcp.Workspace.Selectors.DocumentReference",
        "Roslyn.Workbench.Mcp.Workspace.Selectors.DocumentSelector",
        "Roslyn.Workbench.Mcp.Workspace.Selectors.IWorkspaceSelectorFactory",
        "Roslyn.Workbench.Mcp.Workspace.Selectors.LocationSelector",
        "Roslyn.Workbench.Mcp.Workspace.Selectors.ProjectSelector",
        "Roslyn.Workbench.Mcp.Workspace.Selectors.ResolvedLocation",
        "Roslyn.Workbench.Mcp.Workspace.Selectors.ScopeKind",
        "Roslyn.Workbench.Mcp.Workspace.Selectors.ScopeSelector",
        "Roslyn.Workbench.Mcp.Workspace.Selectors.SnapshotPrecondition",
        "Roslyn.Workbench.Mcp.Workspace.Selectors.SymbolReference",
        "Roslyn.Workbench.Mcp.Workspace.Selectors.SymbolSelector",
        "Roslyn.Workbench.Mcp.Workspace.Selectors.TextSelectionSelector",
        "Roslyn.Workbench.Mcp.Workspace.Selectors.TextSpanRange",
        "Roslyn.Workbench.Mcp.Workspace.Selectors.TextSpanSelector",
        "Roslyn.Workbench.Mcp.Workspace.Selectors.WorkspaceBoundRequest",
        "Roslyn.Workbench.Mcp.Workspace.Selectors.WorkspaceMutationRequest",
        "Roslyn.Workbench.Mcp.Workspace.Selectors.WorkspaceSelector",
        "Roslyn.Workbench.Mcp.Workspace.Validation.NonEmptyGuidAttribute",
        "Roslyn.Workbench.Mcp.Workspace.Validation.ProhibitedUnlessAttribute",
        "Roslyn.Workbench.Mcp.Workspace.Validation.RequiredWhenAttribute",
        "Roslyn.Workbench.Mcp.Workspace.Validation.RequiresAtLeastOneAttribute",
        "Roslyn.Workbench.Mcp.Workspace.Validation.RequiresExactlyOneAttribute",
    ];

    private static readonly string[] _expectedExportedTypes =
    [
        "Roslyn.Workbench.Mcp.Plugins.IMutationContext",
        "Roslyn.Workbench.Mcp.Plugins.IMutationToolHandler",
        "Roslyn.Workbench.Mcp.Plugins.IMutationToolHandler`1",
        "Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration",
        "Roslyn.Workbench.Mcp.Plugins.IPluginServiceConfiguration",
        "Roslyn.Workbench.Mcp.Plugins.IQueryContext",
        "Roslyn.Workbench.Mcp.Plugins.IQueryResponse",
        "Roslyn.Workbench.Mcp.Plugins.IQueryResultCache",
        "Roslyn.Workbench.Mcp.Plugins.IQueryResultCacheKey",
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
        "Roslyn.Workbench.Mcp.Plugins.PluginExecutionResult",
        "Roslyn.Workbench.Mcp.Plugins.PluginExecutionResult`1",
        "Roslyn.Workbench.Mcp.Plugins.QueryToolConfigurationBuilder",
        "Roslyn.Workbench.Mcp.Plugins.RoslynPluginAttribute",
        "Roslyn.Workbench.Mcp.Plugins.RoslynToolAttribute",
        "Roslyn.Workbench.Mcp.Plugins.Services.DependencyCycle",
        "Roslyn.Workbench.Mcp.Plugins.Services.DependencyCycleAnalysisResult",
        "Roslyn.Workbench.Mcp.Plugins.Services.DependencyCycleAnalysisStatus",
        "Roslyn.Workbench.Mcp.Plugins.Services.GraphEdge",
        "Roslyn.Workbench.Mcp.Plugins.Services.GraphNode",
        "Roslyn.Workbench.Mcp.Plugins.Services.ICompilerDiagnosticService",
        "Roslyn.Workbench.Mcp.Plugins.Services.IDependencyAnalysisService",
        "Roslyn.Workbench.Mcp.Plugins.Services.IInspectionContextService",
        "Roslyn.Workbench.Mcp.Plugins.Services.IToolRequestResolver",
        "Roslyn.Workbench.Mcp.Plugins.Services.TestImpactInfo",
        "Roslyn.Workbench.Mcp.Plugins.Services.ToolResolutionResult",
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
    public void GIVEN_AbstractionsAssembly_WHEN_InspectingExportedTypes_THEN_ShouldMatchSupportedThirdPartySurface()
    {
        var exportedTypes = typeof(WorkspaceBoundRequest).Assembly
            .GetExportedTypes()
            .Select(static type => type.FullName)
            .OfType<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();

        exportedTypes.Should().Equal(_expectedAbstractionsExportedTypes);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_AbstractionsAssembly_WHEN_InspectingProductDependencies_THEN_ShouldNotReferenceImplementations()
    {
        var productDependencies = typeof(WorkspaceBoundRequest).Assembly
            .GetReferencedAssemblies()
            .Select(static assembly => assembly.Name)
            .OfType<string>()
            .Where(static name => name.StartsWith("Roslyn.Workbench.Mcp", StringComparison.Ordinal))
            .ToArray();

        productDependencies.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_QueryHandlerContract_WHEN_InspectingResponseConstraint_THEN_ShouldRequireQueryResponseMarker()
    {
        var responseParameter = typeof(IQueryToolHandler<,>).GetGenericArguments()[1];
        var constraints = responseParameter.GetGenericParameterConstraints();

        constraints.Should().ContainSingle().Which.Should().Be<IQueryResponse>();
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_BoundedCollection_WHEN_InspectingResponseContract_THEN_ShouldRemainNestedComponent()
    {
        typeof(IQueryResponse).IsAssignableFrom(typeof(BoundedCollection<string>)).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_PluginsPublicApi_WHEN_InspectingWorkspaceTypes_THEN_ShouldExposeOnlyAbstractions()
    {
        var abstractionsAssembly = typeof(WorkspaceBoundRequest).Assembly;
        var workspaceTypes = typeof(IRoslynPlugin).Assembly
            .GetExportedTypes()
            .SelectMany(GetPublicSignatureTypes)
            .SelectMany(GetTypeClosure)
            .Where(static type => type.Namespace?.StartsWith("Roslyn.Workbench.Mcp.Workspace", StringComparison.Ordinal) == true)
            .Distinct()
            .OrderBy(static type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        workspaceTypes.Should().NotBeEmpty();
        workspaceTypes.Should().OnlyContain(type => type.Assembly == abstractionsAssembly);
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
            nameof(IToolExecutionContext.Snapshot),
            nameof(IToolExecutionContext.ToolExecutionServices),
            nameof(IToolExecutionContext.TransactionRevision),
            nameof(IToolExecutionContext.WorkspaceIdentity),
            nameof(IToolExecutionContext.WorkspacePathService),
            nameof(IToolExecutionContext.WorkspaceResolver),
        ]);

        typeof(IWorkspaceMutationStager).IsAssignableFrom(typeof(IMutationContext)).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_ToolExecutionServices_WHEN_InspectingCapabilities_THEN_ShouldExposeSupportedReadOnlyServices()
    {
        var properties = typeof(IToolExecutionServices).GetProperties();

        properties.Should().OnlyContain(static property => property.SetMethod == null);
        properties.Select(static property => property.Name).Should().BeEquivalentTo(
        [
            nameof(IToolExecutionServices.CompilerDiagnosticService),
            nameof(IToolExecutionServices.DependencyAnalysisService),
            nameof(IToolExecutionServices.InspectionContextService),
            nameof(IToolExecutionServices.ProjectStructureService),
            nameof(IToolExecutionServices.ProjectTargetFrameworkResolver),
            nameof(IToolExecutionServices.ReferenceDiscoveryService),
            nameof(IToolExecutionServices.RequestResolver),
            nameof(IToolExecutionServices.TypeHierarchyService),
            nameof(IToolExecutionServices.WorkspaceSelectorFactory),
        ]);

        var selectorFactoryMethods = typeof(IWorkspaceSelectorFactory)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(static method => method.Name)
            .ToArray();

        selectorFactoryMethods.Should().BeEquivalentTo(
        [
            nameof(IWorkspaceSelectorFactory.CreateCanonicalLocationSelector),
            nameof(IWorkspaceSelectorFactory.CreateLocationSelector),
            nameof(IWorkspaceSelectorFactory.CreateSymbolSelector),
        ]);

        var referenceDiscoveryMethods = typeof(IReferenceDiscoveryService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(static method => method.Name)
            .ToArray();

        referenceDiscoveryMethods.Should().ContainSingle()
            .Which.Should().Be(nameof(IReferenceDiscoveryService.FindReferencesAsync));

        var typeHierarchyMethods = typeof(ITypeHierarchyService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(static method => method.Name)
            .ToArray();

        typeHierarchyMethods.Should().ContainSingle()
            .Which.Should().Be(nameof(ITypeHierarchyService.FindDerivedTypesAsync));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_PluginConfiguration_WHEN_InspectingServiceRegistration_THEN_ShouldExposeOnlyTypedSingletonMappings()
    {
        var configurationProperties = typeof(IPluginConfiguration).GetProperties();
        var serviceMethods = typeof(IPluginServiceConfiguration)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        configurationProperties.Should().ContainSingle(static property =>
            property.Name == nameof(IPluginConfiguration.Services)
            && property.PropertyType == typeof(IPluginServiceConfiguration)
            && property.SetMethod == null);

        serviceMethods.Should().HaveCount(2);
        serviceMethods.Should().OnlyContain(static method =>
            method.Name == nameof(IPluginServiceConfiguration.AddSingleton)
            && method.IsGenericMethodDefinition
            && method.ReturnType == typeof(void));

        serviceMethods
            .Select(static method => method.GetGenericArguments().Length)
            .Should()
            .BeEquivalentTo([1, 2]);
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

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_PublicGenericResults_WHEN_InspectingStaticMembers_THEN_ShouldUseNonGenericCompanions()
    {
        var genericResultTypes = new[]
        {
            typeof(BoundedCollection<>),
            typeof(PluginExecutionResult<>),
            typeof(SelectorResolveResult<>),
            typeof(ToolResolutionResult<,>),
        };

        foreach (var genericResultType in genericResultTypes)
        {
            var publicStaticMethods = genericResultType.GetMethods(
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(static method => !method.IsSpecialName)
                .ToArray();

            publicStaticMethods.Should().BeEmpty();
        }
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
