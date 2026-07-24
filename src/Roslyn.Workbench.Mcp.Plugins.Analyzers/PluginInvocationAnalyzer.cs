using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Roslyn.Workbench.Mcp.Plugins.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PluginInvocationAnalyzer : DiagnosticAnalyzer
{
    private const string _queryHandlerMetadataName = "Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler`2";
    private const string _mutationHandlerMetadataName = "Roslyn.Workbench.Mcp.Plugins.IMutationToolHandler`1";
    private const string _cancellationTokenMetadataName = "System.Threading.CancellationToken";
    private const string _boundedCollectionMetadataName = "Roslyn.Workbench.Mcp.Plugins.BoundedCollection`1";

    private static readonly ImmutableArray<string> _rawCollectionMetadataNames =
    [
        "System.Collections.Generic.IEnumerable`1",
        "System.Collections.Generic.ICollection`1",
        "System.Collections.Generic.IList`1",
        "System.Collections.Generic.IReadOnlyCollection`1",
        "System.Collections.Generic.IReadOnlyList`1",
        "System.Collections.Generic.List`1",
        "System.Collections.Generic.ISet`1",
        "System.Collections.Generic.IReadOnlySet`1",
        "System.Collections.Generic.HashSet`1",
        "System.Collections.Generic.IDictionary`2",
        "System.Collections.Generic.IReadOnlyDictionary`2",
        "System.Collections.Generic.Dictionary`2",
        "System.Collections.Generic.IAsyncEnumerable`1",
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(
            PluginDiagnosticDescriptors.UnobservedCancellationToken,
            PluginDiagnosticDescriptors.UnboundedQueryCollection);

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
        var queryHandlerDefinition = compilation.GetTypeByMetadataName(_queryHandlerMetadataName);
        var mutationHandlerDefinition = compilation.GetTypeByMetadataName(_mutationHandlerMetadataName);
        var cancellationTokenType = compilation.GetTypeByMetadataName(_cancellationTokenMetadataName);
        var boundedCollectionDefinition = compilation.GetTypeByMetadataName(_boundedCollectionMetadataName);
        if (queryHandlerDefinition is null
            || mutationHandlerDefinition is null
            || cancellationTokenType is null
            || boundedCollectionDefinition is null)
        {
            return;
        }

        var rawCollectionDefinitions = ResolveRawCollectionDefinitions(compilation);
        var symbols = new PluginInvocationSymbols(
            queryHandlerDefinition,
            mutationHandlerDefinition,
            cancellationTokenType,
            boundedCollectionDefinition,
            rawCollectionDefinitions);

        context.RegisterOperationBlockStartAction(
            blockContext => RegisterCancellationAnalysis(blockContext, symbols));

        context.RegisterSymbolAction(
            symbolContext => AnalyzeQueryResponse(symbolContext, symbols),
            SymbolKind.NamedType);
    }

    private static ImmutableArray<INamedTypeSymbol> ResolveRawCollectionDefinitions(
        Compilation compilation)
    {
        var definitions = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        foreach (var metadataName in _rawCollectionMetadataNames)
        {
            var definition = compilation.GetTypeByMetadataName(metadataName);
            if (definition is not null)
            {
                definitions.Add(definition);
            }
        }

        return definitions.ToImmutable();
    }

    private static void RegisterCancellationAnalysis(
        OperationBlockStartAnalysisContext context,
        PluginInvocationSymbols symbols)
    {
        if (context.OwningSymbol is not IMethodSymbol method)
        {
            return;
        }

        if (!IsHandlerExecuteMethod(method, symbols))
        {
            return;
        }

        var cancellationParameter = FindCancellationParameter(method, symbols.CancellationTokenType);
        if (cancellationParameter is null)
        {
            return;
        }

        var usage = new CancellationTokenUsageState();
        context.RegisterOperationAction(
            operationContext => AnalyzeParameterReference(
                operationContext,
                cancellationParameter,
                usage),
            OperationKind.ParameterReference);

        context.RegisterOperationBlockEndAction(
            endContext => ReportUnobservedCancellation(
                endContext,
                method,
                cancellationParameter,
                usage));
    }

    private static bool IsHandlerExecuteMethod(
        IMethodSymbol method,
        PluginInvocationSymbols symbols)
    {
        if (method.IsAbstract
            || !string.Equals(method.Name, "ExecuteAsync", StringComparison.Ordinal))
        {
            return false;
        }

        if (method.MethodKind != MethodKind.Ordinary
            && method.MethodKind != MethodKind.ExplicitInterfaceImplementation)
        {
            return false;
        }

        var containingType = method.ContainingType;
        var contracts = PluginHandlerFacts.GetContracts(
            containingType,
            symbols.QueryHandlerDefinition,
            symbols.MutationHandlerDefinition);

        if (!contracts.IsHandlerCandidate)
        {
            return false;
        }

        var hasExecuteContract = false;
        foreach (var contract in contracts.QueryContracts)
        {
            if (ImplementsExecuteContract(
                method,
                containingType,
                contract,
                ref hasExecuteContract))
            {
                return true;
            }
        }

        foreach (var contract in contracts.MutationContracts)
        {
            if (ImplementsExecuteContract(
                method,
                containingType,
                contract,
                ref hasExecuteContract))
            {
                return true;
            }
        }

        return !hasExecuteContract;
    }

    private static bool ImplementsExecuteContract(
        IMethodSymbol method,
        INamedTypeSymbol containingType,
        INamedTypeSymbol contract,
        ref bool hasExecuteContract)
    {
        foreach (var member in contract.GetMembers("ExecuteAsync"))
        {
            if (member is not IMethodSymbol contractMethod)
            {
                continue;
            }

            hasExecuteContract = true;
            var implementation = containingType.FindImplementationForInterfaceMember(contractMethod);
            if (SymbolEqualityComparer.Default.Equals(implementation, method))
            {
                return true;
            }
        }

        return false;
    }

    private static IParameterSymbol? FindCancellationParameter(
        IMethodSymbol method,
        INamedTypeSymbol cancellationTokenType)
    {
        foreach (var parameter in method.Parameters)
        {
            if (SymbolEqualityComparer.Default.Equals(parameter.Type, cancellationTokenType))
            {
                return parameter;
            }
        }

        return null;
    }

    private static void AnalyzeParameterReference(
        OperationAnalysisContext context,
        IParameterSymbol cancellationParameter,
        CancellationTokenUsageState usage)
    {
        var reference = (IParameterReferenceOperation)context.Operation;
        if (!SymbolEqualityComparer.Default.Equals(reference.Parameter, cancellationParameter))
        {
            return;
        }

        if (IsDiscardAssignment(reference))
        {
            return;
        }

        usage.MarkObserved();
    }

    private static bool IsDiscardAssignment(IParameterReferenceOperation reference)
    {
        IOperation current = reference;
        while (current.Parent is IConversionOperation or IParenthesizedOperation)
        {
            current = current.Parent;
        }

        if (current.Parent is not ISimpleAssignmentOperation assignment)
        {
            return false;
        }

        return assignment.Target is IDiscardOperation;
    }

    private static void ReportUnobservedCancellation(
        OperationBlockAnalysisContext context,
        IMethodSymbol method,
        IParameterSymbol cancellationParameter,
        CancellationTokenUsageState usage)
    {
        if (usage.IsObserved)
        {
            return;
        }

        var location = PluginSymbolFacts.FindSourceLocation(cancellationParameter);
        if (location is null)
        {
            return;
        }

        var methodName = method.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        var diagnostic = Diagnostic.Create(
            PluginDiagnosticDescriptors.UnobservedCancellationToken,
            location,
            methodName);

        context.ReportDiagnostic(diagnostic);
    }

    private static void AnalyzeQueryResponse(
        SymbolAnalysisContext context,
        PluginInvocationSymbols symbols)
    {
        if (context.Symbol is not INamedTypeSymbol handlerType)
        {
            return;
        }

        if (handlerType.TypeKind != TypeKind.Class
            || handlerType.IsAbstract
            || handlerType.TypeParameters.Length > 0)
        {
            return;
        }

        var contracts = PluginHandlerFacts.GetContracts(
            handlerType,
            symbols.QueryHandlerDefinition,
            symbols.MutationHandlerDefinition);

        foreach (var queryContract in contracts.QueryContracts)
        {
            var responseType = queryContract.TypeArguments[1];
            if (IsBoundedCollection(responseType, symbols.BoundedCollectionDefinition))
            {
                continue;
            }

            if (IsRawCollection(responseType, symbols))
            {
                ReportRawResponse(context, handlerType, responseType);
                continue;
            }

            AnalyzeResponseProperties(context, responseType, symbols);
        }
    }

    private static void AnalyzeResponseProperties(
        SymbolAnalysisContext context,
        ITypeSymbol responseType,
        PluginInvocationSymbols symbols)
    {
        if (responseType is not INamedTypeSymbol namedResponseType)
        {
            return;
        }

        for (var current = namedResponseType; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                if (member is not IPropertySymbol property)
                {
                    continue;
                }

                if (property.IsStatic || property.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }

                if (!IsRawCollection(property.Type, symbols))
                {
                    continue;
                }

                var location = PluginSymbolFacts.FindSourceLocation(property);
                if (location is null)
                {
                    continue;
                }

                var propertyName = property.ToDisplayString(
                    SymbolDisplayFormat.CSharpErrorMessageFormat);

                var diagnostic = Diagnostic.Create(
                    PluginDiagnosticDescriptors.UnboundedQueryCollection,
                    location,
                    propertyName);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static bool IsRawCollection(
        ITypeSymbol type,
        PluginInvocationSymbols symbols)
    {
        if (type.SpecialType == SpecialType.System_String)
        {
            return false;
        }

        if (type is IArrayTypeSymbol)
        {
            return true;
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        if (IsBoundedCollection(namedType, symbols.BoundedCollectionDefinition))
        {
            return false;
        }

        if (MatchesRawCollectionDefinition(namedType, symbols.RawCollectionDefinitions))
        {
            return true;
        }

        foreach (var interfaceType in namedType.AllInterfaces)
        {
            if (MatchesRawCollectionDefinition(
                interfaceType,
                symbols.RawCollectionDefinitions))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsBoundedCollection(
        ITypeSymbol type,
        INamedTypeSymbol boundedCollectionDefinition)
    {
        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        return SymbolEqualityComparer.Default.Equals(
            namedType.OriginalDefinition,
            boundedCollectionDefinition);
    }

    private static bool MatchesRawCollectionDefinition(
        INamedTypeSymbol type,
        ImmutableArray<INamedTypeSymbol> definitions)
    {
        foreach (var definition in definitions)
        {
            if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, definition))
            {
                return true;
            }
        }

        return false;
    }

    private static void ReportRawResponse(
        SymbolAnalysisContext context,
        INamedTypeSymbol handlerType,
        ITypeSymbol responseType)
    {
        var location = PluginSymbolFacts.FindSourceLocation(handlerType);
        if (location is null)
        {
            return;
        }

        var responseName = responseType.ToDisplayString(
            SymbolDisplayFormat.CSharpErrorMessageFormat);

        var diagnostic = Diagnostic.Create(
            PluginDiagnosticDescriptors.UnboundedQueryCollection,
            location,
            responseName);

        context.ReportDiagnostic(diagnostic);
    }
}
