namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.CodeActions;

public sealed class ListCodeActionsToolTests
{
    [Fact]
    public async Task GIVEN_CodeActionServiceReturnsActions_WHEN_CallingExecute_THEN_ShouldReturnServiceResult()
    {
        var expected = PluginExecutionResult<CodeActionListData>.Success(new CodeActionListData
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
            ReturnedCount = 1,
        });
        var codeActionService = new Mock<ICodeActionService>();
        var context = new QueryContextBuilder()
            .WithCodeActionService(codeActionService.Object)
            .Build();
        var target = new ListCodeActionsTool();

        codeActionService
            .Setup(service => service.ListCodeActionsAsync(
                It.IsAny<ListCodeActionsRequest>(),
                It.IsAny<IQueryContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var request = new ListCodeActionsRequest
        {
            Location = new LocationSelector(),
        };
        var result = await target.ExecuteAsync(request, context, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        codeActionService.Verify(service => service.ListCodeActionsAsync(request, context, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task GIVEN_CodeActionServiceRejectsRequest_WHEN_CallingExecute_THEN_ShouldReturnServiceRejection()
    {
        var expected = PluginExecutionResult<CodeActionListData>.Rejected(new ToolError
        {
            Code = "CodeActionsUnavailable",
            Message = "CodeActionsUnavailable",
        });
        var codeActionService = new Mock<ICodeActionService>();
        var context = new QueryContextBuilder()
            .WithCodeActionService(codeActionService.Object)
            .Build();
        var target = new ListCodeActionsTool();

        codeActionService
            .Setup(service => service.ListCodeActionsAsync(
                It.IsAny<ListCodeActionsRequest>(),
                It.IsAny<IQueryContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var request = new ListCodeActionsRequest
        {
            Location = new LocationSelector(),
        };
        var result = await target.ExecuteAsync(request, context, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        codeActionService.Verify(service => service.ListCodeActionsAsync(request, context, CancellationToken.None), Times.Once);
    }
}
