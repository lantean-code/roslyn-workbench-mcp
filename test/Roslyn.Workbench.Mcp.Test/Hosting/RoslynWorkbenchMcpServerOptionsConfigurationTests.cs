namespace Roslyn.Workbench.Mcp.Test.Hosting;

public sealed class RoslynWorkbenchMcpServerOptionsConfigurationTests
{
    [Fact]
    public async Task GIVEN_ServerOptions_WHEN_Configuring_THEN_ShouldApplyInstructionsAndRoutePluginRequests()
    {
        var pluginRequestHandler = new Mock<IPluginMcpRequestHandler>();
        var listToolsResult = new ListToolsResult
        {
            Tools = [],
        };

        var callToolResult = new CallToolResult
        {
            Content = [],
        };

        var listToolsContext = CreateContext(new ListToolsRequestParams());
        var callToolContext = CreateContext(new CallToolRequestParams
        {
            Name = "plugin-tool",
        });

        var cancellationToken = TestContext.Current.CancellationToken;
        pluginRequestHandler
            .Setup(value => value.ListToolsAsync(listToolsContext, cancellationToken))
            .ReturnsAsync(listToolsResult);

        pluginRequestHandler
            .Setup(value => value.CallToolAsync(callToolContext, cancellationToken))
            .ReturnsAsync(callToolResult);

        var options = new McpServerOptions();
        var policy = OperationalPolicyResolver.Resolve(OperationalMode.AutonomousTrusted);
        var target = new RoslynWorkbenchMcpServerOptionsConfiguration(pluginRequestHandler.Object, policy);

        target.Configure(options);

        var actualListToolsResult = await options.Handlers.ListToolsHandler!(listToolsContext, cancellationToken);
        var actualCallToolResult = await options.Handlers.CallToolHandler!(callToolContext, cancellationToken);

        options.ServerInstructions.Should().Contain("Prefer queries before mutations.");
        options.ServerInstructions.Should().Contain("https://lantean-code.github.io/roslyn-workbench-mcp/");
        options.ServerInstructions.Should().Contain("/agent/");
        options.ServerInstructions.Should().Contain("Workflow: workspace lifecycle");
        options.ServerInstructions.Should().Contain("Everyday inspection: start with get-solution-structure");
        options.ServerInstructions.Should().Contain("Specialist inspection: search for callers/callees");
        options.ServerInstructions.Should().Contain("search for the relevant capability or exact tool name, then read its schema before calling");
        options.ServerInstructions.Should().Contain("they do not hide tools or control client loading");
        actualListToolsResult.Should().BeSameAs(listToolsResult);
        actualCallToolResult.Should().BeSameAs(callToolResult);
        pluginRequestHandler.Verify(
            value => value.ListToolsAsync(listToolsContext, cancellationToken),
            Times.Once);

        pluginRequestHandler.Verify(
            value => value.CallToolAsync(callToolContext, cancellationToken),
            Times.Once);
    }

    [Theory]
    [InlineData((int)OperationalMode.InspectionOnly, "Inspection only; mutations disabled", "does not sandbox build logic")]
    [InlineData((int)OperationalMode.Transactional, "for Host confirmation", "not a Git commit")]
    [InlineData((int)OperationalMode.ApprovalRequired, "transaction-review receiptId for commit approval", "not a Git commit")]
    [InlineData((int)OperationalMode.AutonomousTrusted, "preview then commit or roll back promptly", "not a Git commit")]
    public void GIVEN_SupportedOperationalMode_WHEN_Configuring_THEN_ShouldPublishModeSpecificInstructions(
        int modeValue,
        string expectedWorkflow,
        string expectedEnding)
    {
        var mode = (OperationalMode)modeValue;
        var pluginRequestHandler = new Mock<IPluginMcpRequestHandler>();
        var options = new McpServerOptions();
        var policy = OperationalPolicyResolver.Resolve(mode);
        var target = new RoslynWorkbenchMcpServerOptionsConfiguration(pluginRequestHandler.Object, policy);

        target.Configure(options);

        var instructions = options.ServerInstructions!;
        var initialGuidance = instructions[..instructions.IndexOf("Workflow:", StringComparison.Ordinal)];

        initialGuidance.Should().Contain(expectedWorkflow);
        initialGuidance.Should().Contain(expectedEnding);
        initialGuidance.Should().Contain("fully trusted C# workspaces");
        initialGuidance.Should().Contain("workflow, everyday inspection, specialist inspection");
        initialGuidance.Should().Contain("skill://roslyn-workbench/SKILL.md");
        initialGuidance.Length.Should().BeLessThanOrEqualTo(512);
        instructions.Length.Should().BeLessThanOrEqualTo(4096);
    }

    [Theory]
    [InlineData((int)OperationalMode.Transactional)]
    [InlineData((int)OperationalMode.ApprovalRequired)]
    [InlineData((int)OperationalMode.AutonomousTrusted)]
    public void GIVEN_RequiredCompilerValidation_WHEN_Configuring_THEN_ShouldPublishValidationWithinInstructionBudget(int modeValue)
    {
        var pluginRequestHandler = new Mock<IPluginMcpRequestHandler>();
        var options = new McpServerOptions();
        var policy = OperationalPolicyResolver.Resolve((OperationalMode)modeValue, CommitValidationPolicy.NoNewCompilerErrors);
        var target = new RoslynWorkbenchMcpServerOptionsConfiguration(pluginRequestHandler.Object, policy);

        target.Configure(options);

        var instructions = options.ServerInstructions!;
        var initialGuidance = instructions[..instructions.IndexOf("Workflow:", StringComparison.Ordinal)];

        initialGuidance.Should().Contain("transaction-validate");
        initialGuidance.Should().Contain("review/commit revalidate");
        initialGuidance.Should().Contain("workflow, everyday inspection, specialist inspection");
        initialGuidance.Should().Contain("skill://roslyn-workbench/SKILL.md");
        initialGuidance.Length.Should().BeLessThanOrEqualTo(512);
        instructions.Length.Should().BeLessThanOrEqualTo(4096);
    }

    private static RequestContext<TParams> CreateContext<TParams>(TParams parameters)
    {
        var server = new Mock<McpServer>();
        var request = new JsonRpcRequest
        {
            Method = "test",
        };

        return new RequestContext<TParams>(server.Object, request, parameters);
    }
}
