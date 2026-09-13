using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using Roslyn.Workbench.Mcp.Test.Tools;

namespace Roslyn.Workbench.Mcp.Test.UserInteraction;

public sealed class McpUserInteractionServiceTests
{
    [Fact]
    public async Task GIVEN_ClientWithoutElicitationCapability_WHEN_RequestingInteraction_THEN_ShouldReturnUnavailableWithoutProtocolRequest()
    {
        await using var server = ServerOwnedToolTestSupport.CreateServer(new ClientCapabilities());
        var target = new McpUserInteractionService(server);

        var result = await target.RequestAsync(CreateRequest(), TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(UserInteractionOutcome.Unavailable);
        Mock.Get(server).Verify(
            item => item.SendRequestAsync(It.IsAny<JsonRpcRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_ClientWithoutCapabilityRecord_WHEN_RequestingInteraction_THEN_ShouldReturnUnavailable()
    {
        await using var server = ServerOwnedToolTestSupport.CreateServer();
        Mock.Get(server).SetupGet(item => item.ClientCapabilities).Returns((ClientCapabilities?)null);
        var target = new McpUserInteractionService(server);

        var result = await target.RequestAsync(CreateRequest(), TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(UserInteractionOutcome.Unavailable);
    }

    [Fact]
    public async Task GIVEN_SupportedChoice_WHEN_ClientAcceptsInteraction_THEN_ShouldReturnChoiceAndTranslateSchema()
    {
        var response = CreateResponse("accept", "first");
        await using var server = CreateElicitationServer(response);
        var request = CreateRequest();
        var target = new McpUserInteractionService(server);

        var result = await target.RequestAsync(request, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(UserInteractionOutcome.Accepted);
        result.SelectedValue.Should().Be("first");
        Mock.Get(server).Verify(item => item.SendRequestAsync(
            It.Is<JsonRpcRequest>(protocolRequest => HasExpectedSchema(protocolRequest)),
            TestContext.Current.CancellationToken), Times.Once);
    }

    [Theory]
    [InlineData("decline", (int)UserInteractionOutcome.Declined)]
    [InlineData("cancel", (int)UserInteractionOutcome.Cancelled)]
    public async Task GIVEN_ClientDoesNotAccept_WHEN_RequestingInteraction_THEN_ShouldNormaliseOutcome(
        string action,
        int expectedOutcomeValue)
    {
        await using var server = CreateElicitationServer(CreateResponse(action));
        var target = new McpUserInteractionService(server);

        var result = await target.RequestAsync(CreateRequest(), TestContext.Current.CancellationToken);

        result.Outcome.Should().Be((UserInteractionOutcome)expectedOutcomeValue);
    }

    [Theory]
    [InlineData("unknown", null)]
    [InlineData("accept", null)]
    public async Task GIVEN_InvalidClientResponse_WHEN_RequestingInteraction_THEN_ShouldReturnInvalidResponse(
        string action,
        string? choice)
    {
        var response = CreateResponse(action, choice);
        await using var server = CreateElicitationServer(response);
        var target = new McpUserInteractionService(server);

        var result = await target.RequestAsync(CreateRequest(), TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(UserInteractionOutcome.InvalidResponse);
    }

    [Fact]
    public async Task GIVEN_StructurallyValidUnrecognisedChoice_WHEN_ClientAcceptsInteraction_THEN_ShouldReturnSelectedValue()
    {
        await using var server = CreateElicitationServer(CreateResponse("accept", "unsupported"));
        var target = new McpUserInteractionService(server);

        var result = await target.RequestAsync(CreateRequest(), TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(UserInteractionOutcome.Accepted);
        result.SelectedValue.Should().Be("unsupported");
    }

    [Fact]
    public async Task GIVEN_AcceptedResponseWithoutChoiceProperty_WHEN_RequestingInteraction_THEN_ShouldReturnInvalidResponse()
    {
        var response = new JsonRpcResponse
        {
            Result = new JsonObject
            {
                ["action"] = "accept",
                ["content"] = new JsonObject(),
            },
        };

        await using var server = CreateElicitationServer(response);
        var target = new McpUserInteractionService(server);

        var result = await target.RequestAsync(CreateRequest(), TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(UserInteractionOutcome.InvalidResponse);
    }

    [Fact]
    public async Task GIVEN_AcceptedResponseWithNonStringChoice_WHEN_RequestingInteraction_THEN_ShouldReturnInvalidResponse()
    {
        var response = new JsonRpcResponse
        {
            Result = new JsonObject
            {
                ["action"] = "accept",
                ["content"] = new JsonObject
                {
                    ["choice"] = 1,
                },
            },
        };

        await using var server = CreateElicitationServer(response);
        var target = new McpUserInteractionService(server);

        var result = await target.RequestAsync(CreateRequest(), TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(UserInteractionOutcome.InvalidResponse);
    }

    [Theory]
    [InlineData(false, (int)UserInteractionOutcome.Unavailable)]
    [InlineData(true, (int)UserInteractionOutcome.Failed)]
    public async Task GIVEN_ExpectedProtocolFailure_WHEN_RequestingInteraction_THEN_ShouldNormaliseFailure(
        bool protocolFailure,
        int expectedOutcomeValue)
    {
        await using var server = CreateElicitationServer(CreateResponse("accept", "first"));
        Exception exception = protocolFailure
            ? new McpException("ProtocolFailure")
            : new InvalidOperationException("Unavailable");

        Mock.Get(server)
            .Setup(item => item.SendRequestAsync(It.IsAny<JsonRpcRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var target = new McpUserInteractionService(server);

        var result = await target.RequestAsync(CreateRequest(), TestContext.Current.CancellationToken);

        result.Outcome.Should().Be((UserInteractionOutcome)expectedOutcomeValue);
    }

    private static UserInteractionRequest CreateRequest()
    {
        return new UserInteractionRequest
        {
            Message = "Message",
            Title = "Title",
            Description = "Description",
            Choices =
            [
                new UserInteractionChoice
                {
                    Value = "first",
                    Title = "First",
                },
                new UserInteractionChoice
                {
                    Value = "second",
                    Title = "Second",
                },
            ],
        };
    }

    private static JsonRpcResponse CreateResponse(string action, string? choice = null)
    {
        JsonObject? content = null;
        if (choice is not null)
        {
            content = new JsonObject
            {
                ["choice"] = choice,
            };
        }

        return new JsonRpcResponse
        {
            Result = new JsonObject
            {
                ["action"] = action,
                ["content"] = content,
            },
        };
    }

    private static McpServer CreateElicitationServer(JsonRpcResponse response)
    {
        var capabilities = new ClientCapabilities
        {
            Elicitation = new ElicitationCapability
            {
                Form = new FormElicitationCapability(),
            },
        };

        return ServerOwnedToolTestSupport.CreateServer(capabilities, response);
    }

    private static bool HasExpectedSchema(JsonRpcRequest request)
    {
        if (!string.Equals(request.Method, "elicitation/create", StringComparison.Ordinal))
        {
            return false;
        }

        var parameters = JsonSerializer.SerializeToElement(request.Params);
        var choice = parameters
            .GetProperty("requestedSchema")
            .GetProperty("properties")
            .GetProperty("choice");

        var options = choice.GetProperty("oneOf");

        return parameters.GetProperty("message").GetString() == "Message"
            && choice.GetProperty("title").GetString() == "Title"
            && choice.GetProperty("description").GetString() == "Description"
            && options.GetArrayLength() == 2
            && options[0].GetProperty("const").GetString() == "first"
            && options[0].GetProperty("title").GetString() == "First"
            && options[1].GetProperty("const").GetString() == "second"
            && options[1].GetProperty("title").GetString() == "Second";
    }
}
