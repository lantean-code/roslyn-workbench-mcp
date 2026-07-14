using Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("get-document-options", "Get Document Options", "Returns language, parse and analyzer-config options for a document.")]
internal sealed class GetDocumentOptionsTool : QueryToolHandler<GetDocumentOptionsRequest, DocumentOptionsData>
{
    protected override async ValueTask<PluginExecutionResult<DocumentOptionsData>> ExecuteCoreAsync(GetDocumentOptionsRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var documentResolution = context.ToolExecutionServices.RequestResolver.ResolveDocument<DocumentOptionsData>(request.Document, context);
        if (documentResolution.HasRejection)
        {
            return documentResolution.Rejection;
        }

        var document = documentResolution.Value;
        var parseOptions = document.Project.ParseOptions;
        var data = new DocumentOptionsData
        {
            Document = context.WorkspaceResolver.CreateDocumentReference(document),
            LanguageVersion = parseOptions is CSharpParseOptions csharpParseOptions ? csharpParseOptions.LanguageVersion.ToDisplayString() : parseOptions?.Language ?? string.Empty,
            NullableContext = document.Project.CompilationOptions is CSharpCompilationOptions csharpCompilationOptions ? csharpCompilationOptions.NullableContextOptions.ToString() : string.Empty,
            ParseOptions = InspectionProjectionFactory.CreateParseOptionsInfo(parseOptions),
            AnalyzerConfig = await InspectionProjectionFactory.CreateAnalyzerConfigInfoAsync(document, cancellationToken).ConfigureAwait(false),
        };

        return PluginExecutionResult<DocumentOptionsData>.Success(data);
    }
}
