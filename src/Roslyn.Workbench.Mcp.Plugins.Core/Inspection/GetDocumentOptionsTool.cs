namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("get-document-options", "Get Document Options", "Returns language and optional detailed parse and analyzer-config options for a document.")]
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
        var languageVersion = parseOptions?.Language ?? string.Empty;
        if (parseOptions is CSharpParseOptions csharpParseOptions)
        {
            languageVersion = csharpParseOptions.LanguageVersion.ToDisplayString();
        }

        var nullableContext = string.Empty;
        if (document.Project.CompilationOptions is CSharpCompilationOptions csharpCompilationOptions)
        {
            nullableContext = csharpCompilationOptions.NullableContextOptions.ToString();
        }

        AnalyzerConfigInfo? analyzerConfig = null;
        if (request.IncludeAnalyzerConfig)
        {
            analyzerConfig = await InspectionProjectionFactory.CreateAnalyzerConfigInfoAsync(document, cancellationToken);
        }

        var data = new DocumentOptionsData
        {
            Document = context.WorkspaceResolver.CreateDocumentReference(document),
            LanguageVersion = languageVersion,
            NullableContext = nullableContext,
            ParseOptions = request.IncludeParseOptions
                ? InspectionProjectionFactory.CreateParseOptionsInfo(parseOptions)
                : null,
            AnalyzerConfig = analyzerConfig,
        };

        return PluginExecutionResult.Success(data);
    }
}
