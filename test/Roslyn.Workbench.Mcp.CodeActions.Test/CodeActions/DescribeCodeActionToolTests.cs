namespace Roslyn.Workbench.Mcp.CodeActions.Test.CodeActions;

public sealed class DescribeCodeActionToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterQueryTool()
    {
        var registry = new Mock<ICodeActionToolRegistry>();

        DescribeCodeActionTool.Register(registry.Object);

        registry.Verify(item => item.RegisterQueryTool<DescribeCodeActionRequest, DescribeCodeActionData>(
            It.Is<CodeActionToolMetadata>(metadata =>
                metadata.Name == "describe-code-action"
                && metadata.Title == "Describe Code Action"
                && metadata.Description == "Revalidates one discovered code action and returns its execution descriptor and preflight context."),
            It.IsAny<ICodeActionQueryToolHandler<DescribeCodeActionRequest, DescribeCodeActionData>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_QueryContextReturnsResult_WHEN_CallingExecuteAsync_THEN_ShouldReturnQueryContextResult()
    {
        var target = new DescribeCodeActionTool();
        var context = new Mock<ICodeActionQueryContext>();
        var request = new DescribeCodeActionRequest
        {
            ActionId = "ActionId",
        };
        var expected = CodeActionExecutionResult<DescribeCodeActionData>.Success(new DescribeCodeActionData());

        context
            .Setup(item => item.DescribeCodeActionAsync(request, TestContext.Current.CancellationToken))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }
}
