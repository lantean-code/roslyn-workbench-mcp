namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("get-symbol-attributes", "Get Symbol Attributes", "Returns declared and inherited attributes for a resolved symbol.")]
internal sealed class GetSymbolAttributesTool : QueryToolHandler<GetSymbolAttributesRequest, SymbolAttributesData>
{
    protected override async ValueTask<PluginExecutionResult<SymbolAttributesData>> ExecuteCoreAsync(GetSymbolAttributesRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<SymbolAttributesData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        var symbol = symbolResolution.Value;
        var attributes = new List<AttributeInfo>();
        attributes.AddRange(symbol.GetAttributes().Select(static item => CreateAttributeInfo(item, inherited: false)));

        if (request.IncludeInherited && symbol is INamedTypeSymbol namedType)
        {
            for (var current = namedType.BaseType; current is not null; current = current.BaseType)
            {
                attributes.AddRange(current.GetAttributes().Select(static item => CreateAttributeInfo(item, inherited: true)));
            }
        }

        var orderedAttributes = attributes
            .OrderBy(static item => item.Name, StringComparer.Ordinal)
            .ThenBy(static item => item.Inherited)
            .ToArray();
        var symbolReference = context.WorkspaceResolver.CreateSymbolReference(symbol);

        return PluginExecutionResult<SymbolAttributesData>.Success(new SymbolAttributesData
        {
            Symbol = symbolReference,
            Attributes = ToolExecutionHelpers.CreateBoundedCollection(
                orderedAttributes,
                ToolExecutionHelpers.GetMaxResults(context, request.AttributesLimit)),
        });
    }

    private static AttributeInfo CreateAttributeInfo(AttributeData attributeData, bool inherited)
    {
        return new AttributeInfo
        {
            Name = attributeData.AttributeClass?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) ?? string.Empty,
            Type = InspectionProjectionFactory.CreateTypeInfo(attributeData.AttributeClass),
            Inherited = inherited,
            ConstructorArguments = attributeData.ConstructorArguments.Select(static argument => new AttributeArgumentInfo
            {
                Type = argument.Type?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                Value = argument.Value?.ToString(),
            }).ToArray(),
            NamedArguments = attributeData.NamedArguments.Select(static argument => new AttributeArgumentInfo
            {
                Name = argument.Key,
                Type = argument.Value.Type?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                Value = argument.Value.Value?.ToString(),
            }).ToArray(),
        };
    }
}
