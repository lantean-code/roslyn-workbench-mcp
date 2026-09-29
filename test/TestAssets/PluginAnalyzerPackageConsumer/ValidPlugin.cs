using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Workspace.Selectors;

[RoslynPlugin("example.tools", "Example Tools", PluginApiVersions.V1)]
public sealed class ExamplePlugin : IRoslynPlugin
{
    public void Configure(IPluginConfiguration configuration)
    {
        configuration.AddQueryTool<ExampleQueryTool>();
        configuration.AddMutationTool<ExampleMutationTool>();
    }
}

public sealed record ExampleMutationRequest : WorkspaceMutationRequest
{
    public string RelativeDocumentPath { get; init; } = string.Empty;

    public string SearchText { get; init; } = string.Empty;

    public string ReplacementText { get; init; } = string.Empty;
}

[RoslynTool(
    "example-mutation",
    "Example Mutation",
    "Returns a source mutation candidate.")]
internal sealed class ExampleMutationTool :
    IMutationToolHandler<ExampleMutationRequest>
{
    public ValueTask<PluginExecutionResult<MutationCandidate>> ExecuteAsync(
        ExampleMutationRequest request,
        IMutationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var document = context.CurrentSolution.Projects
            .SelectMany(static project => project.Documents)
            .SingleOrDefault(document => document.FilePath?.EndsWith(
                request.RelativeDocumentPath,
                StringComparison.OrdinalIgnoreCase) == true);

        if (document is null)
        {
            return ValueTask.FromResult(PluginExecutionResult.Rejected<MutationCandidate>(
                new PluginExecutionError
                {
                    Code = "DocumentNotFound",
                    Message = "The requested document was not found.",
                }));
        }

        return CreateCandidateAsync(document, request, cancellationToken);
    }

    private static async ValueTask<PluginExecutionResult<MutationCandidate>> CreateCandidateAsync(
        Document document,
        ExampleMutationRequest request,
        CancellationToken cancellationToken)
    {
        var sourceText = await document.GetTextAsync(cancellationToken);
        var updatedText = sourceText.ToString().Replace(
            request.SearchText,
            request.ReplacementText,
            StringComparison.Ordinal);

        var candidateSolution = document
            .WithText(SourceText.From(updatedText, sourceText.Encoding))
            .Project
            .Solution;

        return PluginExecutionResult.Success(new MutationCandidate
        {
            CandidateSolution = candidateSolution,
            Summary = "Package-built acceptance mutation",
        });
    }
}

public sealed record ExampleQueryRequest : WorkspaceBoundRequest
{
    public string Value { get; init; } = string.Empty;
}

public sealed record ExampleQueryData : IQueryResponse
{
    public string Value { get; init; } = string.Empty;
}

[RoslynTool(
    "example-query",
    "Example Query",
    "Returns an example response.")]
internal sealed class ExampleQueryTool :
    IQueryToolHandler<ExampleQueryRequest, ExampleQueryData>
{
    public ValueTask<PluginExecutionResult<ExampleQueryData>> ExecuteAsync(
        ExampleQueryRequest request,
        IQueryContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var data = new ExampleQueryData
        {
            Value = request.Value,
        };

        var executionResult = PluginExecutionResult.Success<ExampleQueryData>(data);
        var result = ValueTask.FromResult(executionResult);
        return result;
    }
}
