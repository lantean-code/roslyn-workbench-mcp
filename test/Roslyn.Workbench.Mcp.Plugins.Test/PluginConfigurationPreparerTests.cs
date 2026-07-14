namespace Roslyn.Workbench.Mcp.Plugins.Test;

public sealed class PluginConfigurationPreparerTests
{
    private readonly Mock<IPluginHandlerContractResolver> _contractResolver;
    private readonly Mock<IPluginHandlerTypeInspector> _typeInspector;
    private readonly Mock<IPluginHandlerWarningInspector> _warningInspector;
    private readonly PluginMetadata _pluginMetadata;
    private readonly PluginConfigurationPreparer _target;

    public PluginConfigurationPreparerTests()
    {
        _contractResolver = new Mock<IPluginHandlerContractResolver>();
        _typeInspector = new Mock<IPluginHandlerTypeInspector>();
        _warningInspector = new Mock<IPluginHandlerWarningInspector>();
        _typeInspector.Setup(static value => value.Inspect(It.IsAny<Type>())).Returns([]);
        _warningInspector.Setup(static value => value.Inspect(It.IsAny<Type>())).Returns([]);
        _pluginMetadata = new PluginMetadata
        {
            PluginId = "PluginId",
            DisplayName = "DisplayName",
            Version = "1.0.0",
            SupportedApiVersion = PluginApiVersions.V1,
        };
        _target = new PluginConfigurationPreparer(_typeInspector.Object, _contractResolver.Object, _warningInspector.Object);
    }

    [Fact]
    public void GIVEN_AttributeAndFluentMetadata_WHEN_PreparingQuery_THEN_ShouldApplyFluentPrecedence()
    {
        var configuration = new PluginConfiguration();
        _ = configuration.AddQueryTool<AttributedQueryHandler>()
            .WithName("fluent-name")
            .WithTitle("Fluent Title")
            .WithDescription("Fluent description")
            .WithResultSummary("Fluent result");
        configuration.Freeze();
        SetupContract(typeof(IQueryToolHandler<Request, Response>));

        var result = _target.Prepare(_pluginMetadata, configuration);

        var tool = result.Tools.Single().Tool;
        tool.Metadata.Name.Should().Be("fluent-name");
        tool.Metadata.Title.Should().Be("Fluent Title");
        tool.Metadata.Description.Should().Be("Fluent description");
        tool.Metadata.ResultSummary.Should().Be("Fluent result");
        tool.Metadata.Behavior.Destructive.Should().BeFalse();
        tool.Kind.Should().Be(ToolKind.Query);
        tool.RequestType.Should().Be(typeof(Request));
        tool.ResponseType.Should().Be(typeof(Response));
        result.Tools.Single().HandlerFactory().Should().BeOfType<AttributedQueryHandler>();
    }

    [Fact]
    public void GIVEN_AttributeOnlyMutation_WHEN_Preparing_THEN_ShouldPreserveDestructiveMetadata()
    {
        var configuration = new PluginConfiguration();
        _ = configuration.AddMutationTool<AttributedMutationHandler>().IsDestructive();
        configuration.Freeze();
        SetupContract(typeof(IMutationToolHandler<Request>));

        var result = _target.Prepare(_pluginMetadata, configuration);

        var tool = result.Tools.Single().Tool;
        tool.Metadata.Name.Should().Be("attribute-mutation");
        tool.Metadata.Behavior.Destructive.Should().BeTrue();
        tool.Kind.Should().Be(ToolKind.Mutation);
        tool.ResponseType.Should().Be(typeof(MutationData));
    }

    [Fact]
    public void GIVEN_MissingOrDuplicateMetadata_WHEN_Preparing_THEN_ShouldRejectPlugin()
    {
        var missing = new PluginConfiguration();
        _ = missing.AddQueryTool<FluentQueryHandler>();
        missing.Freeze();
        var duplicate = new PluginConfiguration();
        _ = duplicate.AddQueryTool<AttributedQueryHandler>();
        _ = duplicate.AddQueryTool<AttributedQueryHandler>();
        duplicate.Freeze();
        SetupContract(typeof(IQueryToolHandler<Request, Response>));

        var missingResult = _target.Prepare(_pluginMetadata, missing);
        var duplicateResult = _target.Prepare(_pluginMetadata, duplicate);

        missingResult.Diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Id == "PluginToolMetadata"
            && diagnostic.Message.Contains("metadata must provide", StringComparison.Ordinal));
        missingResult.Tools.Should().BeEmpty();
        duplicateResult.Diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Id == "PluginToolName"
            && diagnostic.Message.Contains("more than once", StringComparison.Ordinal));
        duplicateResult.Tools.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_DestructiveQueryMetadata_WHEN_Preparing_THEN_ShouldRejectPlugin()
    {
        var configuration = new PluginConfiguration();
        _ = configuration.AddQueryTool<DestructiveQueryHandler>();
        configuration.Freeze();
        SetupContract(typeof(IQueryToolHandler<Request, Response>));

        var result = _target.Prepare(_pluginMetadata, configuration);

        result.Tools.Should().BeEmpty();
        result.Diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Id == "PluginToolBehaviour"
            && diagnostic.Message.Contains("cannot declare destructive", StringComparison.Ordinal));
    }

    [Fact]
    public void GIVEN_HandlerWarnings_WHEN_Preparing_THEN_ShouldPublishWarningsWithoutDisabling()
    {
        var configuration = new PluginConfiguration();
        _ = configuration.AddQueryTool<AttributedQueryHandler>();
        configuration.Freeze();
        SetupContract(typeof(IQueryToolHandler<Request, Response>));
        _warningInspector.Setup(static value => value.Inspect(typeof(AttributedQueryHandler))).Returns(
        [
            new DiagnosticInfo
            {
                Id = "PluginHandlerState",
                Severity = DiagnosticSeverity.Warning,
                Message = "Warning",
            },
        ]);

        var result = _target.Prepare(_pluginMetadata, configuration);

        result.Tools.Should().ContainSingle();
        result.Diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Id == "PluginHandlerState"
            && diagnostic.Message == "Warning");
        _warningInspector.Verify(static value => value.Inspect(typeof(AttributedQueryHandler)), Times.Once);
    }

    [Fact]
    public void GIVEN_MultipleInvalidTools_WHEN_Preparing_THEN_ShouldAccumulateEveryExpectedError()
    {
        var configuration = new PluginConfiguration();
        _ = configuration.AddQueryTool<FluentQueryHandler>();
        _ = configuration.AddQueryTool<DestructiveQueryHandler>();
        configuration.Freeze();
        SetupContract(typeof(IQueryToolHandler<Request, Response>));

        var result = _target.Prepare(_pluginMetadata, configuration);

        result.Tools.Should().BeEmpty();
        result.Diagnostics.Should().HaveCount(2);
        result.Diagnostics.Select(static diagnostic => diagnostic.Id).Should().Equal(
            "PluginToolMetadata",
            "PluginToolBehaviour");
    }

    [Fact]
    public void GIVEN_OneToolHasIndependentInspectionIssues_WHEN_Preparing_THEN_ShouldPreserveEveryDiagnostic()
    {
        var configuration = new PluginConfiguration();
        _ = configuration.AddQueryTool<FluentQueryHandler>();
        configuration.Freeze();
        _typeInspector.Setup(static value => value.Inspect(typeof(FluentQueryHandler))).Returns(
        [
            CreateDiagnostic("PluginHandlerLifetime", DiagnosticSeverity.Error, "Lifetime error"),
            CreateDiagnostic("PluginHandlerComposition", DiagnosticSeverity.Error, "Composition error"),
        ]);
        _warningInspector.Setup(static value => value.Inspect(typeof(FluentQueryHandler))).Returns(
        [
            CreateDiagnostic("PluginHandlerState", DiagnosticSeverity.Warning, "State warning"),
        ]);
        SetupContractFailure(CreateDiagnostic("PluginHandlerContract", DiagnosticSeverity.Error, "Contract error"));

        var result = _target.Prepare(_pluginMetadata, configuration);

        result.Tools.Should().BeEmpty();
        result.Diagnostics.Select(static diagnostic => diagnostic.Id).Should().Equal(
            "PluginHandlerLifetime",
            "PluginHandlerComposition",
            "PluginHandlerContract",
            "PluginToolMetadata",
            "PluginHandlerState");
    }

    private void SetupContract(Type contract)
    {
        var resolvedContract = contract;
        DiagnosticInfo? diagnostic = null;
        _contractResolver
            .Setup(value => value.TryResolve(
                It.IsAny<ConfiguredToolDefinition>(),
                out resolvedContract,
                out diagnostic))
            .Returns(true);
    }

    private void SetupContractFailure(DiagnosticInfo failure)
    {
        Type? contract = null;
        DiagnosticInfo? diagnostic = failure;
        _contractResolver
            .Setup(value => value.TryResolve(
                It.IsAny<ConfiguredToolDefinition>(),
                out contract,
                out diagnostic))
            .Returns(false);
    }

    private static DiagnosticInfo CreateDiagnostic(string id, DiagnosticSeverity severity, string message)
    {
        return new DiagnosticInfo
        {
            Id = id,
            Severity = severity,
            Message = message,
        };
    }

    public sealed record Request : WorkspaceBoundRequest;

    public sealed record Response;

    [RoslynTool("attribute-query", "Attribute Query", "Attribute query description", ResultSummary = "Attribute result")]
    private sealed class AttributedQueryHandler : IQueryToolHandler<Request, Response>
    {
        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(
            Request request,
            IQueryContext context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult<Response>.Success(new Response()));
        }
    }

    [RoslynTool("attribute-mutation", "Attribute Mutation", "Attribute mutation description", Destructive = true)]
    private sealed class AttributedMutationHandler : IMutationToolHandler<Request>
    {
        public ValueTask<PluginExecutionResult<MutationCandidate>> ExecuteAsync(
            Request request,
            IMutationContext context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult<MutationCandidate>.NoChange());
        }
    }

    private sealed class FluentQueryHandler : IQueryToolHandler<Request, Response>
    {
        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(
            Request request,
            IQueryContext context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult<Response>.Success(new Response()));
        }
    }

    [RoslynTool("destructive-query", "Destructive Query", "Destructive query description", Destructive = true)]
    private sealed class DestructiveQueryHandler : IQueryToolHandler<Request, Response>
    {
        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(
            Request request,
            IQueryContext context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult<Response>.Success(new Response()));
        }
    }
}
