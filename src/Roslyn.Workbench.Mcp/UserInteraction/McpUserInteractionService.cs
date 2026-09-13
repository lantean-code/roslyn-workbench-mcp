using System.Text.Json;
using ModelContextProtocol;

namespace Roslyn.Workbench.Mcp.UserInteraction;

/// <summary>
/// Translates neutral single-select interactions to MCP elicitation requests and outcomes.
/// </summary>
internal sealed class McpUserInteractionService : IUserInteractionService
{
    private const string _choiceProperty = "choice";
    private readonly McpServer _server;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpUserInteractionService"/> class.
    /// </summary>
    /// <param name="server">The active MCP server session.</param>
    public McpUserInteractionService(McpServer server)
    {
        _server = server;
    }

    /// <inheritdoc/>
    public async ValueTask<UserInteractionResult> RequestAsync(
        UserInteractionRequest request,
        CancellationToken cancellationToken)
    {
        if (_server.ClientCapabilities?.Elicitation is null)
        {
            return UserInteractionResult.NotAccepted(UserInteractionOutcome.Unavailable);
        }

        ElicitResult result;
        try
        {
            var elicitation = CreateElicitation(request);
            result = await _server.ElicitAsync(elicitation, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return UserInteractionResult.NotAccepted(UserInteractionOutcome.Unavailable);
        }
        catch (McpException)
        {
            return UserInteractionResult.NotAccepted(UserInteractionOutcome.Failed);
        }

        if (string.Equals(result.Action, "decline", StringComparison.Ordinal))
        {
            return UserInteractionResult.NotAccepted(UserInteractionOutcome.Declined);
        }

        if (string.Equals(result.Action, "cancel", StringComparison.Ordinal))
        {
            return UserInteractionResult.NotAccepted(UserInteractionOutcome.Cancelled);
        }

        if (!result.IsAccepted
            || result.Content is null
            || !result.Content.TryGetValue(_choiceProperty, out var choiceElement)
            || choiceElement.ValueKind != JsonValueKind.String)
        {
            return UserInteractionResult.NotAccepted(UserInteractionOutcome.InvalidResponse);
        }

        var selectedValue = choiceElement.ToString();
        return UserInteractionResult.Accepted(selectedValue);
    }

    private static ElicitRequestParams CreateElicitation(UserInteractionRequest request)
    {
        var choices = request.Choices
            .Select(static choice => new ElicitRequestParams.EnumSchemaOption
            {
                Const = choice.Value,
                Title = choice.Title,
            })
            .ToArray();

        var choice = new ElicitRequestParams.TitledSingleSelectEnumSchema
        {
            Title = request.Title,
            Description = request.Description,
            OneOf = choices,
        };

        return new ElicitRequestParams
        {
            Message = request.Message,
            RequestedSchema = new ElicitRequestParams.RequestSchema
            {
                Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>(StringComparer.Ordinal)
                {
                    [_choiceProperty] = choice,
                },
                Required = [_choiceProperty],
            },
        };
    }
}
