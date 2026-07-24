using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Roslyn.Workbench.Mcp.Plugins.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PluginEntryPointAnalyzer : DiagnosticAnalyzer
{
    private const string _pluginAttributeMetadataName = "Roslyn.Workbench.Mcp.Plugins.RoslynPluginAttribute";
    private const string _pluginInterfaceMetadataName = "Roslyn.Workbench.Mcp.Plugins.IRoslynPlugin";
    private const string _pluginApiVersionsMetadataName = "Roslyn.Workbench.Mcp.Plugins.PluginApiVersions";
    private const string _toolAttributeMetadataName = "Roslyn.Workbench.Mcp.Plugins.RoslynToolAttribute";
    private const string _queryHandlerMetadataName = "Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler`2";
    private const string _mutationHandlerMetadataName = "Roslyn.Workbench.Mcp.Plugins.IMutationToolHandler`1";
    private const string _supportedApiVersionFieldName = "V1";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(
            PluginDiagnosticDescriptors.PluginEntryPointContract,
            PluginDiagnosticDescriptors.MultiplePluginEntryPoints,
            PluginDiagnosticDescriptors.UnsupportedPluginApiVersion,
            PluginDiagnosticDescriptors.BlankPluginIdentity,
            PluginDiagnosticDescriptors.ToolMetadataWithoutHandler);

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(RegisterCompilationActions);
    }

    private static void RegisterCompilationActions(CompilationStartAnalysisContext context)
    {
        var symbols = ResolveSymbols(context.Compilation);
        if (symbols is null)
        {
            return;
        }

        var markedEntryPoints = new ConcurrentBag<MarkedPluginEntryPoint>();
        context.RegisterSymbolAction(
            symbolContext => AnalyzeType(symbolContext, symbols, markedEntryPoints),
            SymbolKind.NamedType);

        context.RegisterCompilationEndAction(
            compilationContext => AnalyzeMarkedEntryPointCount(
                compilationContext,
                markedEntryPoints));
    }

    private static PluginEntryPointSymbols? ResolveSymbols(Compilation compilation)
    {
        var pluginAttribute = compilation.GetTypeByMetadataName(_pluginAttributeMetadataName);
        var pluginInterface = compilation.GetTypeByMetadataName(_pluginInterfaceMetadataName);
        var pluginApiVersions = compilation.GetTypeByMetadataName(_pluginApiVersionsMetadataName);
        var toolAttribute = compilation.GetTypeByMetadataName(_toolAttributeMetadataName);
        var queryHandlerDefinition = compilation.GetTypeByMetadataName(_queryHandlerMetadataName);
        var mutationHandlerDefinition = compilation.GetTypeByMetadataName(_mutationHandlerMetadataName);
        if (pluginAttribute is null
            || pluginInterface is null
            || pluginApiVersions is null
            || toolAttribute is null
            || queryHandlerDefinition is null
            || mutationHandlerDefinition is null)
        {
            return null;
        }

        var supportedApiVersion = ResolveSupportedApiVersion(pluginApiVersions);
        if (supportedApiVersion is null)
        {
            return null;
        }

        var symbols = new PluginEntryPointSymbols(
            pluginAttribute,
            pluginInterface,
            toolAttribute,
            queryHandlerDefinition,
            mutationHandlerDefinition,
            supportedApiVersion);

        return symbols;
    }

    private static string? ResolveSupportedApiVersion(INamedTypeSymbol pluginApiVersions)
    {
        foreach (var member in pluginApiVersions.GetMembers(_supportedApiVersionFieldName))
        {
            if (member is IFieldSymbol { ConstantValue: string version })
            {
                return version;
            }
        }

        return null;
    }

    private static void AnalyzeType(
        SymbolAnalysisContext context,
        PluginEntryPointSymbols symbols,
        ConcurrentBag<MarkedPluginEntryPoint> markedEntryPoints)
    {
        if (context.Symbol is not INamedTypeSymbol type)
        {
            return;
        }

        var sourceLocation = PluginSymbolFacts.FindSourceLocation(type);
        if (sourceLocation is null)
        {
            return;
        }

        var pluginAttribute = PluginAttributeFacts.FindAttribute(type, symbols.PluginAttribute);
        var implementsPlugin = PluginSymbolFacts.ImplementsInterface(type, symbols.PluginInterface);
        var isConcretePlugin = IsConcretePlugin(type, implementsPlugin);
        if (pluginAttribute is not null)
        {
            var attributeLocation = PluginAttributeFacts.GetApplicationLocation(
                pluginAttribute,
                context.CancellationToken);

            if (attributeLocation is not null)
            {
                var entryPoint = new MarkedPluginEntryPoint(type, attributeLocation);
                markedEntryPoints.Add(entryPoint);
            }

            if (!isConcretePlugin)
            {
                var location = attributeLocation ?? sourceLocation;
                ReportEntryPointContract(context, type, location);
            }

            AnalyzePluginMetadata(context, pluginAttribute, symbols);
        }
        else if (isConcretePlugin)
        {
            ReportEntryPointContract(context, type, sourceLocation);
        }

        AnalyzeToolMetadata(context, type, symbols);
    }

    private static bool IsConcretePlugin(INamedTypeSymbol type, bool implementsPlugin)
    {
        if (!implementsPlugin || type.TypeKind != TypeKind.Class)
        {
            return false;
        }

        return !type.IsAbstract && type.TypeParameters.Length == 0;
    }

    private static void ReportEntryPointContract(
        SymbolAnalysisContext context,
        INamedTypeSymbol type,
        Location location)
    {
        var typeName = type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        var diagnostic = Diagnostic.Create(
            PluginDiagnosticDescriptors.PluginEntryPointContract,
            location,
            typeName);

        context.ReportDiagnostic(diagnostic);
    }

    private static void AnalyzePluginMetadata(
        SymbolAnalysisContext context,
        AttributeData attribute,
        PluginEntryPointSymbols symbols)
    {
        AnalyzePluginIdentity(context, attribute, parameterOrdinal: 0, "ID");
        AnalyzePluginIdentity(context, attribute, parameterOrdinal: 1, "display name");

        if (attribute.ConstructorArguments.Length <= 2)
        {
            return;
        }

        var declaredVersion = attribute.ConstructorArguments[2].Value as string;
        if (string.Equals(
            declaredVersion,
            symbols.SupportedApiVersion,
            StringComparison.Ordinal))
        {
            return;
        }

        var location = PluginAttributeFacts.GetConstructorArgumentLocation(
            attribute,
            parameterOrdinal: 2,
            context.CancellationToken);

        if (location is null)
        {
            return;
        }

        var diagnostic = Diagnostic.Create(
            PluginDiagnosticDescriptors.UnsupportedPluginApiVersion,
            location,
            symbols.SupportedApiVersion);

        context.ReportDiagnostic(diagnostic);
    }

    private static void AnalyzePluginIdentity(
        SymbolAnalysisContext context,
        AttributeData attribute,
        int parameterOrdinal,
        string metadataName)
    {
        if (attribute.ConstructorArguments.Length <= parameterOrdinal)
        {
            return;
        }

        var value = attribute.ConstructorArguments[parameterOrdinal].Value as string;
        if (!string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var location = PluginAttributeFacts.GetConstructorArgumentLocation(
            attribute,
            parameterOrdinal,
            context.CancellationToken);

        if (location is null)
        {
            return;
        }

        var diagnostic = Diagnostic.Create(
            PluginDiagnosticDescriptors.BlankPluginIdentity,
            location,
            metadataName);

        context.ReportDiagnostic(diagnostic);
    }

    private static void AnalyzeToolMetadata(
        SymbolAnalysisContext context,
        INamedTypeSymbol type,
        PluginEntryPointSymbols symbols)
    {
        var toolAttribute = PluginAttributeFacts.FindAttribute(type, symbols.ToolAttribute);
        if (toolAttribute is null)
        {
            return;
        }

        var contracts = PluginHandlerFacts.GetContracts(
            type,
            symbols.QueryHandlerDefinition,
            symbols.MutationHandlerDefinition);

        if (!contracts.QueryContracts.IsEmpty || !contracts.MutationContracts.IsEmpty)
        {
            return;
        }

        var location = PluginAttributeFacts.GetApplicationLocation(
            toolAttribute,
            context.CancellationToken);

        if (location is null)
        {
            return;
        }

        var typeName = type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        var diagnostic = Diagnostic.Create(
            PluginDiagnosticDescriptors.ToolMetadataWithoutHandler,
            location,
            typeName);

        context.ReportDiagnostic(diagnostic);
    }

    private static void AnalyzeMarkedEntryPointCount(
        CompilationAnalysisContext context,
        ConcurrentBag<MarkedPluginEntryPoint> markedEntryPoints)
    {
        if (markedEntryPoints.Count <= 1)
        {
            return;
        }

        foreach (var entryPoint in markedEntryPoints)
        {
            var typeName = entryPoint.Type.ToDisplayString(
                SymbolDisplayFormat.CSharpErrorMessageFormat);

            var diagnostic = Diagnostic.Create(
                PluginDiagnosticDescriptors.MultiplePluginEntryPoints,
                entryPoint.AttributeLocation,
                typeName);

            context.ReportDiagnostic(diagnostic);
        }
    }
}
