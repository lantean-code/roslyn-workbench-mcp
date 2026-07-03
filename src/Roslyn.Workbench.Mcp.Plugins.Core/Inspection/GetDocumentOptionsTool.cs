using Roslyn.Workbench.Mcp.Contracts.Inspection;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

internal sealed class GetDocumentOptionsTool : QueryToolHandler<GetDocumentOptionsRequest, DocumentOptionsData>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "get-document-options",
        Title = "Get Document Options",
        Description = "Returns language, parse and analyzer-config options for a document.",
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new GetDocumentOptionsTool());
    }

    protected override async ValueTask<PluginExecutionResult<DocumentOptionsData>> ExecuteCoreAsync(GetDocumentOptionsRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var documentResolution = ToolExecutionHelpers.ResolveDocument<DocumentOptionsData>(request.Document, context);
        if (documentResolution.HasRejection)
        {
            return documentResolution.Rejection;
        }

        var document = documentResolution.Value;
        var parseOptions = document.Project.ParseOptions;
        var data = new DocumentOptionsData
        {
            Document = context.Resolver.CreateDocumentReference(document),
            LanguageVersion = parseOptions is CSharpParseOptions csharpParseOptions ? csharpParseOptions.LanguageVersion.ToDisplayString() : parseOptions?.Language ?? string.Empty,
            NullableContext = document.Project.CompilationOptions is CSharpCompilationOptions csharpCompilationOptions ? csharpCompilationOptions.NullableContextOptions.ToString() : string.Empty,
            ParseOptions = InspectionProjectionFactory.CreateParseOptionsInfo(parseOptions),
            AnalyzerConfig = await InspectionProjectionFactory.CreateAnalyzerConfigInfoAsync(document, cancellationToken).ConfigureAwait(false),
        };

        return ToolExecutionHelpers.EnsureWithinSize(context, data);
    }
}
