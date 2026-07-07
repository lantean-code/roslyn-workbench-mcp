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
        });
        var context = new QueryContextBuilder()
            .WithListCodeActionsAsync((request, cancellationToken) =>
            {
                request.Should().BeEquivalentTo(new ListCodeActionsRequest
                {
                    Location = new LocationSelector(),
                });
                cancellationToken.Should().Be(CancellationToken.None);
                return ValueTask.FromResult(expected);
            })
            .Build();
        var target = new ListCodeActionsTool();

        var request = new ListCodeActionsRequest
        {
            Location = new LocationSelector(),
        };
        var result = await target.ExecuteAsync(request, context, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_CodeActionServiceRejectsRequest_WHEN_CallingExecute_THEN_ShouldReturnServiceRejection()
    {
        var expected = PluginExecutionResult<CodeActionListData>.Rejected(new ToolError
        {
            Code = "CodeActionsUnavailable",
            Message = "CodeActionsUnavailable",
        });
        var context = new QueryContextBuilder()
            .WithListCodeActionsAsync((request, cancellationToken) =>
            {
                request.Should().BeEquivalentTo(new ListCodeActionsRequest
                {
                    Location = new LocationSelector(),
                });
                cancellationToken.Should().Be(CancellationToken.None);
                return ValueTask.FromResult(expected);
            })
            .Build();
        var target = new ListCodeActionsTool();

        var request = new ListCodeActionsRequest
        {
            Location = new LocationSelector(),
        };
        var result = await target.ExecuteAsync(request, context, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }
}
