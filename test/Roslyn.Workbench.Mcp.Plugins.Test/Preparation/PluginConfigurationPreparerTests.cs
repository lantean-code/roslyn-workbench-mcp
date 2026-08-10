namespace Roslyn.Workbench.Mcp.Plugins.Test.Preparation;

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
        configuration.AddQueryTool<AttributedQueryHandler>()
            .WithName("fluent-name")
            .WithTitle("Fluent Title")
            .WithDescription("Fluent description")
            .WithResultSummary("Fluent result");

        configuration.Freeze();
        SetupContract(typeof(IQueryToolHandler<Request, Response>));

        var result = _target.Prepare(
            _pluginMetadata,
            configuration,
            PluginContractAccessibility.PublicOnly);

        var tool = result.Tools.Single().Tool;
        tool.Metadata.Name.Should().Be("fluent-name");
        tool.Metadata.Title.Should().Be("Fluent Title");
        tool.Metadata.Description.Should().Be("Fluent description");
        tool.Metadata.ResultSummary.Should().Be("Fluent result");
        tool.Metadata.Behavior.Destructive.Should().BeFalse();
        tool.Kind.Should().Be(ToolKind.Query);
        tool.RequestType.Should().Be<Request>();
        tool.ResponseType.Should().Be<Response>();
        result.Tools.Single().HandlerFactory().Should().BeOfType<AttributedQueryHandler>();
    }

    [Fact]
    public void GIVEN_AttributeOnlyMutation_WHEN_Preparing_THEN_ShouldPreserveDestructiveMetadata()
    {
        var configuration = new PluginConfiguration();
        configuration.AddMutationTool<AttributedMutationHandler>().IsDestructive();
        configuration.Freeze();
        SetupContract(typeof(IMutationToolHandler<Request>));

        var result = _target.Prepare(
            _pluginMetadata,
            configuration,
            PluginContractAccessibility.PublicOnly);

        var tool = result.Tools.Single().Tool;
        tool.Metadata.Name.Should().Be("attribute-mutation");
        tool.Metadata.Behavior.Destructive.Should().BeTrue();
        tool.Kind.Should().Be(ToolKind.Mutation);
        tool.ResponseType.Should().Be<MutationData>();
    }

    [Fact]
    public void GIVEN_MissingOrDuplicateMetadata_WHEN_Preparing_THEN_ShouldRejectPlugin()
    {
        var missing = new PluginConfiguration();
        missing.AddQueryTool<FluentQueryHandler>();
        missing.Freeze();
        var duplicate = new PluginConfiguration();
        duplicate.AddQueryTool<AttributedQueryHandler>();
        duplicate.AddQueryTool<AttributedQueryHandler>();
        duplicate.Freeze();
        SetupContract(typeof(IQueryToolHandler<Request, Response>));

        var missingResult = _target.Prepare(
            _pluginMetadata,
            missing,
            PluginContractAccessibility.PublicOnly);

        var duplicateResult = _target.Prepare(
            _pluginMetadata,
            duplicate,
            PluginContractAccessibility.PublicOnly);

        missingResult.Diagnostics.Should().Contain(diagnostic =>
            diagnostic.Id == "PluginToolMetadata"
            && diagnostic.Message.Contains("metadata must provide", StringComparison.Ordinal));
        missingResult.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "PluginToolName");
        missingResult.Diagnostics.Should().HaveCount(2);

        missingResult.Tools.Should().BeEmpty();
        duplicateResult.Diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Id == "PluginToolName"
            && diagnostic.Message.Contains("more than once", StringComparison.Ordinal));

        duplicateResult.Tools.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_AttributeNameIsNotProtocolCompatible_WHEN_Preparing_THEN_ShouldRejectPlugin()
    {
        var configuration = new PluginConfiguration();
        configuration.AddQueryTool<InvalidNameQueryHandler>();
        configuration.Freeze();
        SetupContract(typeof(IQueryToolHandler<Request, Response>));

        var result = _target.Prepare(
            _pluginMetadata,
            configuration,
            PluginContractAccessibility.PublicOnly);

        result.Tools.Should().BeEmpty();
        result.Diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Id == "PluginToolName"
            && diagnostic.Message.Contains("1 to 128", StringComparison.Ordinal));
    }

    [Fact]
    public void GIVEN_FluentNameIsNotProtocolCompatible_WHEN_Preparing_THEN_ShouldRejectFinalMergedName()
    {
        var configuration = new PluginConfiguration();
        configuration.AddQueryTool<AttributedQueryHandler>().WithName(new string('a', 129));
        configuration.Freeze();
        SetupContract(typeof(IQueryToolHandler<Request, Response>));

        var result = _target.Prepare(
            _pluginMetadata,
            configuration,
            PluginContractAccessibility.PublicOnly);

        result.Tools.Should().BeEmpty();
        result.Diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == "PluginToolName");
    }

    [Fact]
    public void GIVEN_DestructiveQueryMetadata_WHEN_Preparing_THEN_ShouldRejectPlugin()
    {
        var configuration = new PluginConfiguration();
        configuration.AddQueryTool<DestructiveQueryHandler>();
        configuration.Freeze();
        SetupContract(typeof(IQueryToolHandler<Request, Response>));

        var result = _target.Prepare(
            _pluginMetadata,
            configuration,
            PluginContractAccessibility.PublicOnly);

        result.Tools.Should().BeEmpty();
        result.Diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Id == "PluginToolBehaviour"
            && diagnostic.Message.Contains("cannot declare destructive", StringComparison.Ordinal));
    }

    [Fact]
    public void GIVEN_HandlerWarnings_WHEN_Preparing_THEN_ShouldPublishWarningsWithoutDisabling()
    {
        var configuration = new PluginConfiguration();
        configuration.AddQueryTool<AttributedQueryHandler>();
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

        var result = _target.Prepare(
            _pluginMetadata,
            configuration,
            PluginContractAccessibility.PublicOnly);

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
        configuration.AddQueryTool<FluentQueryHandler>();
        configuration.AddQueryTool<DestructiveQueryHandler>();
        configuration.Freeze();
        SetupContract(typeof(IQueryToolHandler<Request, Response>));

        var result = _target.Prepare(
            _pluginMetadata,
            configuration,
            PluginContractAccessibility.PublicOnly);

        result.Tools.Should().BeEmpty();
        result.Diagnostics.Should().HaveCount(3);
        result.Diagnostics.Select(static diagnostic => diagnostic.Id).Should().Equal(
            "PluginToolMetadata",
            "PluginToolName",
            "PluginToolBehaviour");
    }

    [Fact]
    public void GIVEN_OneToolHasIndependentInspectionIssues_WHEN_Preparing_THEN_ShouldPreserveEveryDiagnostic()
    {
        var configuration = new PluginConfiguration();
        configuration.AddQueryTool<FluentQueryHandler>();
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

        var result = _target.Prepare(
            _pluginMetadata,
            configuration,
            PluginContractAccessibility.PublicOnly);

        result.Tools.Should().BeEmpty();
        result.Diagnostics.Select(static diagnostic => diagnostic.Id).Should().Equal(
            "PluginHandlerLifetime",
            "PluginHandlerComposition",
            "PluginHandlerContract",
            "PluginToolMetadata",
            "PluginToolName",
            "PluginHandlerState");
    }

    private void SetupContract(Type contract)
    {
        var resolvedContract = contract;
        DiagnosticInfo? diagnostic = null;
        _contractResolver
            .Setup(value => value.TryResolve(
                It.IsAny<ConfiguredToolDefinition>(),
                It.IsAny<PluginContractAccessibility>(),
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
                It.IsAny<PluginContractAccessibility>(),
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

#pragma warning disable CA1812 // Request fixture is consumed as closed generic registration metadata.
    private sealed record Request : WorkspaceMutationRequest;
#pragma warning restore CA1812

    private sealed record Response;

    [RoslynTool("attribute-query", "Attribute Query", "Attribute query description", ResultSummary = "Attribute result")]
    private sealed class AttributedQueryHandler : IQueryToolHandler<Request, Response>
    {
        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(
            Request request,
            IQueryContext context,
            CancellationToken cancellationToken)
        {
            var response = new Response();
            var result = PluginExecutionResult.Success(response);
            return ValueTask.FromResult(result);
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
            return ValueTask.FromResult(PluginExecutionResult.NoChange<MutationCandidate>());
        }
    }

    private sealed class FluentQueryHandler : IQueryToolHandler<Request, Response>
    {
        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(
            Request request,
            IQueryContext context,
            CancellationToken cancellationToken)
        {
            var response = new Response();
            var result = PluginExecutionResult.Success(response);
            return ValueTask.FromResult(result);
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
            var response = new Response();
            var result = PluginExecutionResult.Success(response);
            return ValueTask.FromResult(result);
        }
    }

    [RoslynTool("invalid name", "Invalid Name", "Invalid name description")]
    private sealed class InvalidNameQueryHandler : IQueryToolHandler<Request, Response>
    {
        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(
            Request request,
            IQueryContext context,
            CancellationToken cancellationToken)
        {
            var response = new Response();
            var result = PluginExecutionResult.Success(response);
            return ValueTask.FromResult(result);
        }
    }
}
