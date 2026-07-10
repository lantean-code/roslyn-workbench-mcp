namespace Roslyn.Workbench.Mcp.CodeActions.Test.CodeActions;

public sealed class ListCodeActionsToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterQueryTool()
    {
        var registry = new Mock<ICodeActionToolRegistry>();

        ListCodeActionsTool.Register(registry.Object);

        registry.Verify(item => item.RegisterQueryTool<ListCodeActionsRequest, CodeActionListData>(
            It.Is<CodeActionToolMetadata>(metadata =>
                metadata.Name == "list-code-actions"
                && metadata.Title == "List Code Actions"
                && metadata.Description == "Lists applicable code actions and code fixes at a target location."),
            It.IsAny<ICodeActionQueryToolHandler<ListCodeActionsRequest, CodeActionListData>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_QueryContextReturnsResult_WHEN_CallingExecuteAsync_THEN_ShouldReturnQueryContextResult()
    {
        var target = new ListCodeActionsTool();
        var context = new Mock<ICodeActionQueryContext>();
        var request = new ListCodeActionsRequest
        {
            Location = new LocationSelector(),
        };
        var expected = CodeActionExecutionResult<CodeActionListData>.Success(new CodeActionListData
        {
            Actions =
            [
                new CodeActionInfo
                {
                    ActionId = "ActionId",
                    Title = "Title",
                    ProviderId = "ProviderId",
                    ExpiresAt = "2000-01-01T00:00:00Z",
                },
            ],
        });

        context
            .Setup(item => item.ListCodeActionsAsync(request, TestContext.Current.CancellationToken))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
        context.Verify(item => item.ListCodeActionsAsync(request, TestContext.Current.CancellationToken), Times.Once);
    }
}
