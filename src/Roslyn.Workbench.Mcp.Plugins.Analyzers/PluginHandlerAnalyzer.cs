using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Roslyn.Workbench.Mcp.Plugins.Validation;

namespace Roslyn.Workbench.Mcp.Plugins.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PluginHandlerAnalyzer : DiagnosticAnalyzer
{
    private const string _queryHandlerMarkerMetadataName = "Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler";
    private const string _mutationHandlerMarkerMetadataName = "Roslyn.Workbench.Mcp.Plugins.IMutationToolHandler";
    private const string _queryHandlerMetadataName = "Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler`2";
    private const string _mutationHandlerMetadataName = "Roslyn.Workbench.Mcp.Plugins.IMutationToolHandler`1";
    private const string _pluginAttributeMetadataName = "Roslyn.Workbench.Mcp.Plugins.RoslynPluginAttribute";
    private const string _roslynToolAttributeMetadataName = "Roslyn.Workbench.Mcp.Plugins.RoslynToolAttribute";
    private const string _disposableMetadataName = "System.IDisposable";
    private const string _asyncDisposableMetadataName = "System.IAsyncDisposable";
    private const string _importAttributeMetadataName = "System.Composition.ImportAttribute";
    private const string _importManyAttributeMetadataName = "System.Composition.ImportManyAttribute";
    private const string _importingConstructorAttributeMetadataName = "System.Composition.ImportingConstructorAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(
            PluginDiagnosticDescriptors.HandlerContract,
            PluginDiagnosticDescriptors.DisposableHandler,
            PluginDiagnosticDescriptors.HandlerMefImport,
            PluginDiagnosticDescriptors.PublicTransportContract,
            PluginDiagnosticDescriptors.HandlerInstanceState,
            PluginDiagnosticDescriptors.MutableStaticHandlerState,
            PluginDiagnosticDescriptors.DisposableHandlerField,
            PluginDiagnosticDescriptors.DestructiveQueryHandler,
            PluginDiagnosticDescriptors.InvalidToolName);

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
        var compilation = context.Compilation;
        var queryHandlerMarker = compilation.GetTypeByMetadataName(_queryHandlerMarkerMetadataName);
        var mutationHandlerMarker = compilation.GetTypeByMetadataName(_mutationHandlerMarkerMetadataName);
        var queryHandlerDefinition = compilation.GetTypeByMetadataName(_queryHandlerMetadataName);
        var mutationHandlerDefinition = compilation.GetTypeByMetadataName(_mutationHandlerMetadataName);
        var pluginAttribute = compilation.GetTypeByMetadataName(_pluginAttributeMetadataName);
        var roslynToolAttribute = compilation.GetTypeByMetadataName(_roslynToolAttributeMetadataName);
        var disposableInterface = compilation.GetTypeByMetadataName(_disposableMetadataName);
        var asyncDisposableInterface = compilation.GetTypeByMetadataName(_asyncDisposableMetadataName);
        if (queryHandlerMarker is null
            || mutationHandlerMarker is null
            || queryHandlerDefinition is null
            || mutationHandlerDefinition is null
            || pluginAttribute is null
            || roslynToolAttribute is null
            || disposableInterface is null
            || asyncDisposableInterface is null)
        {
            return;
        }

        var rootNamespace = compilation.Assembly.GlobalNamespace;
        var compilationDeclaresPlugin = PluginSymbolFacts.CompilationDeclaresPlugin(
            rootNamespace,
            pluginAttribute);

        var importAttribute = compilation.GetTypeByMetadataName(_importAttributeMetadataName);
        var importManyAttribute = compilation.GetTypeByMetadataName(_importManyAttributeMetadataName);
        var importingConstructorAttribute = compilation.GetTypeByMetadataName(_importingConstructorAttributeMetadataName);

        var symbols = new PluginHandlerSymbols(
            queryHandlerMarker,
            mutationHandlerMarker,
            queryHandlerDefinition,
            mutationHandlerDefinition,
            roslynToolAttribute,
            disposableInterface,
            asyncDisposableInterface,
            importAttribute,
            importManyAttribute,
            importingConstructorAttribute,
            compilationDeclaresPlugin);

        context.RegisterSymbolAction(
            symbolContext => AnalyzeHandler(symbolContext, symbols),
            SymbolKind.NamedType);
    }

    private static void AnalyzeHandler(SymbolAnalysisContext context, PluginHandlerSymbols symbols)
    {
        if (context.Symbol is not INamedTypeSymbol handlerType)
        {
            return;
        }

        if (handlerType.TypeKind != TypeKind.Class)
        {
            return;
        }

        if (handlerType.IsAbstract || handlerType.TypeParameters.Length > 0)
        {
            return;
        }

        var contracts = PluginHandlerFacts.GetContracts(
            handlerType,
            symbols.QueryHandlerDefinition,
            symbols.MutationHandlerDefinition,
            symbols.QueryHandlerMarker,
            symbols.MutationHandlerMarker);

        if (!contracts.IsHandlerCandidate)
        {
            return;
        }

        AnalyzeContractShape(context, handlerType, contracts);
        AnalyzeDisposableLifetime(context, handlerType, symbols);
        AnalyzeMefImports(context, handlerType, symbols);
        AnalyzeTransportContracts(context, handlerType, contracts, symbols);
        AnalyzeHandlerState(context, handlerType, symbols);
        AnalyzeDestructiveQueryMetadata(context, handlerType, contracts, symbols);
        AnalyzeToolName(context, handlerType, symbols);
    }

    private static void AnalyzeToolName(
        SymbolAnalysisContext context,
        INamedTypeSymbol handlerType,
        PluginHandlerSymbols symbols)
    {
        foreach (var attribute in handlerType.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, symbols.RoslynToolAttribute)
                || attribute.ConstructorArguments.Length == 0
                || attribute.ConstructorArguments[0].Value is not string toolName
                || PluginToolNamePolicy.IsValid(toolName))
            {
                continue;
            }

            var location = GetAttributeLocation(attribute, handlerType, context.CancellationToken);
            Report(context, PluginDiagnosticDescriptors.InvalidToolName, location, toolName);
        }
    }

    private static void AnalyzeContractShape(
        SymbolAnalysisContext context,
        INamedTypeSymbol handlerType,
        PluginHandlerContractSet contracts)
    {
        if (contracts.IsValid)
        {
            return;
        }

        var location = GetRequiredSourceLocation(handlerType);
        var handlerName = GetDisplayName(handlerType);
        Report(context, PluginDiagnosticDescriptors.HandlerContract, location, handlerName);
    }

    private static void AnalyzeDisposableLifetime(
        SymbolAnalysisContext context,
        INamedTypeSymbol handlerType,
        PluginHandlerSymbols symbols)
    {
        var isDisposable = PluginSymbolFacts.IsOrImplementsInterface(
            handlerType,
            symbols.DisposableInterface);

        var isAsyncDisposable = PluginSymbolFacts.IsOrImplementsInterface(
            handlerType,
            symbols.AsyncDisposableInterface);

        if (!isDisposable && !isAsyncDisposable)
        {
            return;
        }

        var location = GetRequiredSourceLocation(handlerType);
        var handlerName = GetDisplayName(handlerType);
        Report(context, PluginDiagnosticDescriptors.DisposableHandler, location, handlerName);
    }

    private static void AnalyzeMefImports(
        SymbolAnalysisContext context,
        INamedTypeSymbol handlerType,
        PluginHandlerSymbols symbols)
    {
        var hasMetadataImport = false;
        for (var current = handlerType; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                var memberAttributes = member.GetAttributes();

                AnalyzeImportAttributes(
                    context,
                    handlerType,
                    member,
                    memberAttributes,
                    symbols,
                    ref hasMetadataImport);

                if (member is not IMethodSymbol method)
                {
                    continue;
                }

                foreach (var parameter in method.Parameters)
                {
                    var parameterAttributes = parameter.GetAttributes();

                    AnalyzeImportAttributes(
                        context,
                        handlerType,
                        parameter,
                        parameterAttributes,
                        symbols,
                        ref hasMetadataImport);
                }
            }
        }

        if (hasMetadataImport)
        {
            var location = GetRequiredSourceLocation(handlerType);
            var handlerName = GetDisplayName(handlerType);
            Report(context, PluginDiagnosticDescriptors.HandlerMefImport, location, handlerName);
        }
    }

    private static void AnalyzeImportAttributes(
        SymbolAnalysisContext context,
        INamedTypeSymbol handlerType,
        ISymbol attributedSymbol,
        ImmutableArray<AttributeData> attributes,
        PluginHandlerSymbols symbols,
        ref bool hasMetadataImport)
    {
        foreach (var attribute in attributes)
        {
            if (!IsMefImport(attribute.AttributeClass, symbols))
            {
                continue;
            }

            var syntaxReference = attribute.ApplicationSyntaxReference;
            if (syntaxReference is null)
            {
                hasMetadataImport = true;
                continue;
            }

            var syntax = syntaxReference.GetSyntax(context.CancellationToken);
            var location = syntax.GetLocation();
            var memberName = GetDisplayName(attributedSymbol);
            Report(context, PluginDiagnosticDescriptors.HandlerMefImport, location, memberName);
        }
    }

    private static bool IsMefImport(INamedTypeSymbol? attributeType, PluginHandlerSymbols symbols)
    {
        if (attributeType is null)
        {
            return false;
        }

        if (SymbolEqualityComparer.Default.Equals(attributeType, symbols.ImportAttribute))
        {
            return true;
        }

        if (SymbolEqualityComparer.Default.Equals(attributeType, symbols.ImportManyAttribute))
        {
            return true;
        }

        var isImportingConstructor = SymbolEqualityComparer.Default.Equals(
            attributeType,
            symbols.ImportingConstructorAttribute);

        return isImportingConstructor;
    }

    private static void AnalyzeTransportContracts(
        SymbolAnalysisContext context,
        INamedTypeSymbol handlerType,
        PluginHandlerContractSet contracts,
        PluginHandlerSymbols symbols)
    {
        if (!symbols.CompilationDeclaresPlugin)
        {
            return;
        }

        AnalyzeTransportContracts(context, handlerType, contracts.QueryContracts);
        AnalyzeTransportContracts(context, handlerType, contracts.MutationContracts);
    }

    private static void AnalyzeTransportContracts(
        SymbolAnalysisContext context,
        INamedTypeSymbol handlerType,
        ImmutableArray<INamedTypeSymbol> contracts)
    {
        foreach (var contract in contracts)
        {
            foreach (var contractType in contract.TypeArguments)
            {
                var inaccessibleType = FindFirstNonPublicContractType(contractType);
                if (inaccessibleType is null)
                {
                    continue;
                }

                var location = PluginSymbolFacts.FindSourceLocation(inaccessibleType);
                location ??= GetRequiredSourceLocation(handlerType);

                var typeName = GetDisplayName(inaccessibleType);
                Report(context, PluginDiagnosticDescriptors.PublicTransportContract, location, typeName);
            }
        }
    }

    private static ITypeSymbol? FindFirstNonPublicContractType(ITypeSymbol contractType)
    {
        if (contractType is IArrayTypeSymbol arrayType)
        {
            var inaccessibleElementType = FindFirstNonPublicContractType(arrayType.ElementType);
            return inaccessibleElementType;
        }

        if (contractType is not INamedTypeSymbol namedType)
        {
            return null;
        }

        if (namedType.ContainingType is not null)
        {
            var inaccessibleContainingType = FindFirstNonPublicContractType(namedType.ContainingType);
            if (inaccessibleContainingType is not null)
            {
                return inaccessibleContainingType;
            }
        }

        if (namedType.DeclaredAccessibility != Accessibility.Public)
        {
            return namedType;
        }

        foreach (var typeArgument in namedType.TypeArguments)
        {
            var inaccessibleTypeArgument = FindFirstNonPublicContractType(typeArgument);
            if (inaccessibleTypeArgument is not null)
            {
                return inaccessibleTypeArgument;
            }
        }

        return null;
    }

    private static void AnalyzeHandlerState(
        SymbolAnalysisContext context,
        INamedTypeSymbol handlerType,
        PluginHandlerSymbols symbols)
    {
        var hasMetadataInstanceState = false;
        var hasMetadataStaticState = false;
        var hasMetadataDisposableField = false;
        for (var current = handlerType; current is not null; current = current.BaseType)
        {
            AnalyzeFields(
                context,
                current,
                symbols,
                ref hasMetadataInstanceState,
                ref hasMetadataStaticState,
                ref hasMetadataDisposableField);

            AnalyzePropertiesAndEvents(
                context,
                current,
                ref hasMetadataInstanceState);
        }

        var handlerName = GetDisplayName(handlerType);
        var handlerLocation = GetRequiredSourceLocation(handlerType);
        if (hasMetadataInstanceState)
        {
            Report(
                context,
                PluginDiagnosticDescriptors.HandlerInstanceState,
                handlerLocation,
                handlerName);
        }

        if (hasMetadataStaticState)
        {
            Report(
                context,
                PluginDiagnosticDescriptors.MutableStaticHandlerState,
                handlerLocation,
                handlerName);
        }

        if (hasMetadataDisposableField)
        {
            Report(
                context,
                PluginDiagnosticDescriptors.DisposableHandlerField,
                handlerLocation,
                handlerName);
        }
    }

    private static void AnalyzeFields(
        SymbolAnalysisContext context,
        INamedTypeSymbol declaringType,
        PluginHandlerSymbols symbols,
        ref bool hasMetadataInstanceState,
        ref bool hasMetadataStaticState,
        ref bool hasMetadataDisposableField)
    {
        foreach (var member in declaringType.GetMembers())
        {
            if (member is not IFieldSymbol field || field.IsImplicitlyDeclared)
            {
                continue;
            }

            var location = PluginSymbolFacts.FindSourceLocation(field);
            var fieldName = GetDisplayName(field);
            if (!field.IsStatic)
            {
                ReportOrRememberMetadata(
                    context,
                    PluginDiagnosticDescriptors.HandlerInstanceState,
                    location,
                    fieldName,
                    ref hasMetadataInstanceState);
            }

            if (field.IsStatic && !field.IsConst && !field.IsReadOnly)
            {
                ReportOrRememberMetadata(
                    context,
                    PluginDiagnosticDescriptors.MutableStaticHandlerState,
                    location,
                    fieldName,
                    ref hasMetadataStaticState);
            }

            if (IsDisposableType(field.Type, symbols))
            {
                ReportOrRememberMetadata(
                    context,
                    PluginDiagnosticDescriptors.DisposableHandlerField,
                    location,
                    fieldName,
                    ref hasMetadataDisposableField);
            }
        }
    }

    private static void AnalyzePropertiesAndEvents(
        SymbolAnalysisContext context,
        INamedTypeSymbol declaringType,
        ref bool hasMetadataInstanceState)
    {
        foreach (var member in declaringType.GetMembers())
        {
            if (!IntroducesInstanceState(member))
            {
                continue;
            }

            var location = PluginSymbolFacts.FindSourceLocation(member);
            var memberName = GetDisplayName(member);
            ReportOrRememberMetadata(
                context,
                PluginDiagnosticDescriptors.HandlerInstanceState,
                location,
                memberName,
                ref hasMetadataInstanceState);
        }
    }

    private static bool IntroducesInstanceState(ISymbol member)
    {
        if (member is IPropertySymbol property)
        {
            return !property.IsStatic
                && !property.IsImplicitlyDeclared
                && property.SetMethod is not null;
        }

        if (member is IEventSymbol eventSymbol)
        {
            return !eventSymbol.IsStatic && !eventSymbol.IsImplicitlyDeclared;
        }

        return false;
    }

    private static bool IsDisposableType(ITypeSymbol type, PluginHandlerSymbols symbols)
    {
        if (PluginSymbolFacts.IsOrImplementsInterface(type, symbols.DisposableInterface))
        {
            return true;
        }

        var isAsyncDisposable = PluginSymbolFacts.IsOrImplementsInterface(
            type,
            symbols.AsyncDisposableInterface);

        return isAsyncDisposable;
    }

    private static void AnalyzeDestructiveQueryMetadata(
        SymbolAnalysisContext context,
        INamedTypeSymbol handlerType,
        PluginHandlerContractSet contracts,
        PluginHandlerSymbols symbols)
    {
        if (contracts.QueryContracts.IsEmpty)
        {
            return;
        }

        foreach (var attribute in handlerType.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(
                attribute.AttributeClass,
                symbols.RoslynToolAttribute))
            {
                continue;
            }

            if (!DeclaresDestructiveBehaviour(attribute))
            {
                continue;
            }

            var location = GetAttributeLocation(
                attribute,
                handlerType,
                context.CancellationToken);

            var handlerName = GetDisplayName(handlerType);
            Report(context, PluginDiagnosticDescriptors.DestructiveQueryHandler, location, handlerName);
        }
    }

    private static bool DeclaresDestructiveBehaviour(AttributeData attribute)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            var name = argument.Key;
            var value = argument.Value;
            if (string.Equals(name, "Destructive", StringComparison.Ordinal)
                && value.Value is true)
            {
                return true;
            }
        }

        return false;
    }

    private static Location GetAttributeLocation(
        AttributeData attribute,
        ISymbol fallbackSymbol,
        CancellationToken cancellationToken)
    {
        var syntaxReference = attribute.ApplicationSyntaxReference;
        if (syntaxReference is not null)
        {
            var syntax = syntaxReference.GetSyntax(cancellationToken);
            var location = syntax.GetLocation();
            return location;
        }

        var fallbackLocation = GetRequiredSourceLocation(fallbackSymbol);
        return fallbackLocation;
    }

    private static Location GetRequiredSourceLocation(ISymbol symbol)
    {
        var location = PluginSymbolFacts.FindSourceLocation(symbol);
        if (location is not null)
        {
            return location;
        }

        return Location.None;
    }

    private static string GetDisplayName(ISymbol symbol)
    {
        var displayName = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        return displayName;
    }

    private static void ReportOrRememberMetadata(
        SymbolAnalysisContext context,
        DiagnosticDescriptor descriptor,
        Location? location,
        string symbolName,
        ref bool hasMetadataMember)
    {
        if (location is null)
        {
            hasMetadataMember = true;
            return;
        }

        Report(context, descriptor, location, symbolName);
    }

    private static void Report(
        SymbolAnalysisContext context,
        DiagnosticDescriptor descriptor,
        Location location,
        string symbolName)
    {
        var diagnostic = Diagnostic.Create(descriptor, location, symbolName);
        context.ReportDiagnostic(diagnostic);
    }
}
