using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Workspace.Selectors;

namespace Roslyn.Workbench.Mcp.HostMutationPluginFixture;

[RoslynPlugin("host.valid.mutation", "Host Valid Mutation Plugin", PluginApiVersions.V1)]
public sealed class HostValidMutationPlugin : IRoslynPlugin
{
    public void Configure(IPluginConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        configuration.AddMutationTool<Handler>();
    }

    public sealed record Request : WorkspaceMutationRequest
    {
        public string Summary { get; init; } = string.Empty;

        public string? ControlDirectory { get; init; }

        public string? RelativeDocumentPath { get; init; }

        public string? SearchText { get; init; }

        public string? ReplacementText { get; init; }
    }

    [RoslynTool("host-valid-mutation", "Host Valid Mutation", "Returns a stable host test mutation proposal.")]
    private sealed class Handler : IMutationToolHandler<Request>
    {
        public async ValueTask<PluginExecutionResult<MutationCandidate>> ExecuteAsync(Request request, IMutationContext context, CancellationToken cancellationToken)
        {
            await PluginFixtureControl.WaitForReleaseAsync(request.ControlDirectory, cancellationToken);

            var candidateSolution = context.CurrentSolution;
            if (string.IsNullOrWhiteSpace(request.RelativeDocumentPath))
            {
                return PluginExecutionResult.NoChange<MutationCandidate>();
            }

            var document = FindDocument(candidateSolution, request.RelativeDocumentPath);
            if (document is null)
            {
                return PluginExecutionResult.Rejected<MutationCandidate>(
                    new PluginExecutionError
                    {
                        Code = "DocumentNotFound",
                        Message = "The requested document was not found.",
                    });
            }

            var sourceText = await document.GetTextAsync(cancellationToken);
            var currentText = sourceText.ToString();
            var searchText = request.SearchText ?? string.Empty;
            if (!currentText.Contains(searchText, StringComparison.Ordinal))
            {
                return PluginExecutionResult.Rejected<MutationCandidate>(
                    new PluginExecutionError
                    {
                        Code = "TextNotFound",
                        Message = "The requested text was not found in the document.",
                    });
            }

            var replacementText = request.ReplacementText ?? string.Empty;
            var updatedText = currentText.Replace(searchText, replacementText, StringComparison.Ordinal);
            var updatedSourceText = SourceText.From(updatedText, sourceText.Encoding);
            candidateSolution = document.WithText(updatedSourceText).Project.Solution;

            var candidate = new MutationCandidate
            {
                CandidateSolution = candidateSolution,
                Summary = request.Summary,
            };

            var result = PluginExecutionResult.Success(candidate);
            return result;
        }

        private static Document? FindDocument(Solution solution, string relativePath)
        {
            var normalizedRelativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            var suffix = $"{Path.DirectorySeparatorChar}{normalizedRelativePath}";

            foreach (var project in solution.Projects)
            {
                foreach (var document in project.Documents)
                {
                    if (document.FilePath is not null
                        && document.FilePath.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        return document;
                    }
                }
            }

            return null;
        }
    }
}
