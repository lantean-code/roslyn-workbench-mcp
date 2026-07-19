namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("get-symbol-attributes", "Get Symbol Attributes", "Returns declared and inherited attributes for a resolved symbol.")]
internal sealed class GetSymbolAttributesTool : QueryToolHandler<GetSymbolAttributesRequest, SymbolAttributesData>
{
    protected override async ValueTask<PluginExecutionResult<SymbolAttributesData>> ExecuteCoreAsync(GetSymbolAttributesRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<SymbolAttributesData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        var symbol = symbolResolution.Value;
        var discoveredAttributes = new List<(AttributeData Attribute, bool Inherited)>();
        foreach (var attribute in symbol.GetAttributes())
        {
            discoveredAttributes.Add((attribute, false));
        }

        if (request.IncludeInherited && symbol is INamedTypeSymbol namedType)
        {
            for (var current = namedType.BaseType; current is not null; current = current.BaseType)
            {
                foreach (var attribute in current.GetAttributes())
                {
                    discoveredAttributes.Add((attribute, true));
                }
            }
        }

        var orderedAttributes = discoveredAttributes
            .OrderBy(static item => item.Attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static item => item.Inherited);

        var attributes = new List<AttributeInfo>();
        var hasMore = false;
        foreach (var (attribute, inherited) in orderedAttributes)
        {
            if (attributes.Count == request.EffectiveAttributesLimit)
            {
                hasMore = true;
                break;
            }

            attributes.Add(CreateAttributeInfo(attribute, inherited));
        }

        var symbolReference = context.WorkspaceResolver.CreateSymbolReference(symbol);
        var data = new SymbolAttributesData
        {
            Symbol = symbolReference,
            Attributes = ToolExecutionHelpers.CreatePreboundedCollection(attributes, hasMore),
        };

        return PluginExecutionResult<SymbolAttributesData>.Success(data);
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
