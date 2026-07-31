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
        var nonMultipleAttributeTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var attribute in symbol.GetAttributes())
        {
            discoveredAttributes.Add((attribute, false));
            if (attribute.AttributeClass is { } attributeClass && !AllowsMultiple(attributeClass))
            {
                nonMultipleAttributeTypes.Add(attributeClass);
            }
        }

        if (request.IncludeInherited)
        {
            for (var current = GetOverriddenOrBaseSymbol(symbol); current is not null; current = GetOverriddenOrBaseSymbol(current))
            {
                foreach (var attribute in current.GetAttributes())
                {
                    if (attribute.AttributeClass is not { } attributeClass
                        || !IsInherited(attributeClass)
                        || !AllowsMultiple(attributeClass) && !nonMultipleAttributeTypes.Add(attributeClass))
                    {
                        continue;
                    }

                    discoveredAttributes.Add((attribute, true));
                }
            }
        }

        var orderedAttributes = discoveredAttributes
            .OrderBy(static item => item.Attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static item => item.Inherited);

        var attributes = new List<AttributeInfo>();
        foreach (var (attribute, inherited) in orderedAttributes)
        {
            if (attributes.Count == request.EffectiveAttributesLimit)
            {
                break;
            }

            attributes.Add(CreateAttributeInfo(attribute, inherited));
        }

        var symbolReference = context.WorkspaceResolver.CreateSymbolReference(symbol);
        var data = new SymbolAttributesData
        {
            Symbol = symbolReference,
            Attributes = BoundedCollection.CreatePrebounded(attributes, discoveredAttributes.Count),
        };

        return PluginExecutionResult.Success(data);
    }

    private static bool AllowsMultiple(INamedTypeSymbol attributeClass)
    {
        var usage = GetAttributeUsage(attributeClass);
        return GetBooleanNamedArgument(usage, "AllowMultiple", defaultValue: false);
    }

    private static AttributeData? GetAttributeUsage(INamedTypeSymbol attributeClass)
    {
        return attributeClass.GetAttributes().FirstOrDefault(static attribute =>
            string.Equals(
                attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                "global::System.AttributeUsageAttribute",
                StringComparison.Ordinal));
    }

    private static bool GetBooleanNamedArgument(AttributeData? attribute, string name, bool defaultValue)
    {
        if (attribute is null)
        {
            return defaultValue;
        }

        foreach (var argument in attribute.NamedArguments)
        {
            if (string.Equals(argument.Key, name, StringComparison.Ordinal)
                && argument.Value.Value is bool value)
            {
                return value;
            }
        }

        return defaultValue;
    }

    private static ISymbol? GetOverriddenOrBaseSymbol(ISymbol symbol)
    {
        return symbol switch
        {
            INamedTypeSymbol namedType => namedType.BaseType,
            IMethodSymbol method => method.OverriddenMethod,
            IPropertySymbol property => property.OverriddenProperty,
            IEventSymbol eventSymbol => eventSymbol.OverriddenEvent,
            _ => null,
        };
    }

    private static bool IsInherited(INamedTypeSymbol attributeClass)
    {
        var usage = GetAttributeUsage(attributeClass);
        return GetBooleanNamedArgument(usage, "Inherited", defaultValue: true);
    }

    private static AttributeInfo CreateAttributeInfo(AttributeData attributeData, bool inherited)
    {
        var constructorArguments = new List<AttributeArgumentInfo>();
        foreach (var argument in attributeData.ConstructorArguments)
        {
            constructorArguments.Add(new AttributeArgumentInfo
            {
                Type = argument.Type?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                Value = argument.Value?.ToString(),
            });
        }

        var namedArguments = new List<AttributeArgumentInfo>();
        foreach (var argument in attributeData.NamedArguments)
        {
            namedArguments.Add(new AttributeArgumentInfo
            {
                Name = argument.Key,
                Type = argument.Value.Type?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                Value = argument.Value.Value?.ToString(),
            });
        }

        return new AttributeInfo
        {
            Name = attributeData.AttributeClass?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) ?? string.Empty,
            Type = InspectionProjectionFactory.CreateTypeInfo(attributeData.AttributeClass),
            Inherited = inherited,
            ConstructorArguments = constructorArguments,
            NamedArguments = namedArguments,
        };
    }
}
