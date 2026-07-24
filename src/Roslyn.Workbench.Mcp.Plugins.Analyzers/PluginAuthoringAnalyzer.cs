using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Roslyn.Workbench.Mcp.Plugins.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PluginAuthoringAnalyzer : DiagnosticAnalyzer
{
    private const string _workspaceMetadataName = "Microsoft.CodeAnalysis.Workspace";
    private const string _pluginAttributeMetadataName = "Roslyn.Workbench.Mcp.Plugins.RoslynPluginAttribute";
    private const string _pluginContractMetadataName = "Roslyn.Workbench.Mcp.Plugins.IRoslynPlugin";
    private const string _pluginConfigurationMetadataName = "Roslyn.Workbench.Mcp.Plugins.IPluginConfiguration";
    private const string _queryHandlerMetadataName = "Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler";
    private const string _mutationHandlerMetadataName = "Roslyn.Workbench.Mcp.Plugins.IMutationToolHandler";
    private const string _builderMetadataName = "Roslyn.Workbench.Mcp.Plugins.ToolConfigurationBuilder`1";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(
            PluginDiagnosticDescriptors.DirectWorkspaceMutation,
            PluginDiagnosticDescriptors.LiveWorkspaceSolution,
            PluginDiagnosticDescriptors.AsynchronousPluginConfiguration,
            PluginDiagnosticDescriptors.RetainedPluginConfiguration);

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
        var workspaceType = compilation.GetTypeByMetadataName(_workspaceMetadataName);
        var pluginAttributeType = compilation.GetTypeByMetadataName(_pluginAttributeMetadataName);
        var pluginContractType = compilation.GetTypeByMetadataName(_pluginContractMetadataName);
        var pluginConfigurationType = compilation.GetTypeByMetadataName(_pluginConfigurationMetadataName);
        var queryHandlerType = compilation.GetTypeByMetadataName(_queryHandlerMetadataName);
        var mutationHandlerType = compilation.GetTypeByMetadataName(_mutationHandlerMetadataName);
        var builderType = compilation.GetTypeByMetadataName(_builderMetadataName);
        if (workspaceType is null
            || pluginAttributeType is null
            || pluginContractType is null
            || pluginConfigurationType is null
            || queryHandlerType is null
            || mutationHandlerType is null
            || builderType is null)
        {
            return;
        }

        var rootNamespace = compilation.Assembly.GlobalNamespace;
        var compilationDeclaresPlugin = PluginSymbolFacts.CompilationDeclaresPlugin(
            rootNamespace,
            pluginAttributeType);

        var symbols = new PluginAuthoringSymbols(
            workspaceType,
            pluginContractType,
            pluginConfigurationType,
            queryHandlerType,
            mutationHandlerType,
            builderType,
            compilationDeclaresPlugin);

        context.RegisterOperationAction(
            operationContext => AnalyzeInvocation(operationContext, symbols),
            OperationKind.Invocation);

        context.RegisterOperationAction(
            operationContext => AnalyzePropertyReference(operationContext, symbols),
            OperationKind.PropertyReference);

        context.RegisterOperationAction(
            operationContext => AnalyzeAssignment(operationContext, symbols),
            OperationKind.SimpleAssignment);

        context.RegisterOperationAction(
            operationContext => AnalyzeEscapingAnonymousFunction(operationContext, symbols),
            OperationKind.AnonymousFunction);

        context.RegisterSymbolAction(
            symbolContext => AnalyzeMethod(symbolContext, symbols),
            SymbolKind.Method);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, PluginAuthoringSymbols symbols)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        if (!string.Equals(method.Name, "TryApplyChanges", StringComparison.Ordinal)
            || method.Parameters.Length != 1
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, symbols.WorkspaceType)
            || !IsWorkspaceRuleActive(context.ContainingSymbol, symbols))
        {
            return;
        }

        var location = invocation.Syntax.GetLocation();
        var diagnostic = Diagnostic.Create(
            PluginDiagnosticDescriptors.DirectWorkspaceMutation,
            location);

        context.ReportDiagnostic(diagnostic);
    }

    private static void AnalyzePropertyReference(OperationAnalysisContext context, PluginAuthoringSymbols symbols)
    {
        var propertyReference = (IPropertyReferenceOperation)context.Operation;
        var property = propertyReference.Property;
        if (!string.Equals(property.Name, "CurrentSolution", StringComparison.Ordinal)
            || !SymbolEqualityComparer.Default.Equals(property.ContainingType, symbols.WorkspaceType)
            || !IsWorkspaceRuleActive(context.ContainingSymbol, symbols))
        {
            return;
        }

        var location = propertyReference.Syntax.GetLocation();
        var diagnostic = Diagnostic.Create(
            PluginDiagnosticDescriptors.LiveWorkspaceSolution,
            location);

        context.ReportDiagnostic(diagnostic);
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context, PluginAuthoringSymbols symbols)
    {
        var method = (IMethodSymbol)context.Symbol;
        if (!method.IsAsync)
        {
            return;
        }

        if (!ImplementsConfigure(method, symbols.PluginContractType))
        {
            return;
        }

        var location = PluginSymbolFacts.FindSourceLocation(method);
        if (location is null)
        {
            return;
        }

        var diagnostic = Diagnostic.Create(
            PluginDiagnosticDescriptors.AsynchronousPluginConfiguration,
            location);

        context.ReportDiagnostic(diagnostic);
    }

    private static void AnalyzeAssignment(OperationAnalysisContext context, PluginAuthoringSymbols symbols)
    {
        var assignment = (ISimpleAssignmentOperation)context.Operation;
        if (assignment.Target is not (IFieldReferenceOperation or IPropertyReferenceOperation))
        {
            return;
        }

        var assignedType = GetUnconvertedType(assignment.Value);
        if (!IsStartupConfigurationType(assignedType, symbols))
        {
            return;
        }

        var location = assignment.Syntax.GetLocation();
        var diagnostic = Diagnostic.Create(
            PluginDiagnosticDescriptors.RetainedPluginConfiguration,
            location);

        context.ReportDiagnostic(diagnostic);
    }

    private static void AnalyzeEscapingAnonymousFunction(
        OperationAnalysisContext context,
        PluginAuthoringSymbols symbols)
    {
        var anonymousFunction = (IAnonymousFunctionOperation)context.Operation;
        if (!CanEscape(anonymousFunction))
        {
            return;
        }

        if (!CapturesStartupConfiguration(anonymousFunction, symbols))
        {
            return;
        }

        var location = anonymousFunction.Syntax.GetLocation();
        var diagnostic = Diagnostic.Create(
            PluginDiagnosticDescriptors.RetainedPluginConfiguration,
            location);

        context.ReportDiagnostic(diagnostic);
    }

    private static bool IsWorkspaceRuleActive(ISymbol containingSymbol, PluginAuthoringSymbols symbols)
    {
        if (symbols.CompilationDeclaresPlugin)
        {
            return true;
        }

        var containingType = containingSymbol.ContainingType;
        if (containingType is null)
        {
            return false;
        }

        if (PluginSymbolFacts.ImplementsInterface(containingType, symbols.QueryHandlerType))
        {
            return true;
        }

        return PluginSymbolFacts.ImplementsInterface(containingType, symbols.MutationHandlerType);
    }

    private static bool ImplementsConfigure(IMethodSymbol method, INamedTypeSymbol pluginContractType)
    {
        foreach (var member in pluginContractType.GetMembers("Configure"))
        {
            if (member is not IMethodSymbol interfaceMethod)
            {
                continue;
            }

            var containingType = method.ContainingType;
            var implementation = containingType.FindImplementationForInterfaceMember(interfaceMethod);
            if (SymbolEqualityComparer.Default.Equals(implementation, method))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsStartupConfigurationType(ITypeSymbol? type, PluginAuthoringSymbols symbols)
    {
        if (type is null)
        {
            return false;
        }

        if (SymbolEqualityComparer.Default.Equals(type, symbols.PluginConfigurationType))
        {
            return true;
        }

        for (var current = type as INamedTypeSymbol; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(
                current.OriginalDefinition,
                symbols.BuilderType))
            {
                return true;
            }
        }

        return false;
    }

    private static ITypeSymbol? GetUnconvertedType(IOperation operation)
    {
        while (operation is IConversionOperation conversion)
        {
            operation = conversion.Operand;
        }

        return operation.Type;
    }

    private static bool CanEscape(IAnonymousFunctionOperation anonymousFunction)
    {
        IOperation operation = anonymousFunction;
        while (operation.Parent is IConversionOperation or IDelegateCreationOperation)
        {
            operation = operation.Parent;
        }

        return operation.Parent is IArgumentOperation
            or IReturnOperation
            or ISimpleAssignmentOperation;
    }

    private static bool CapturesStartupConfiguration(
        IAnonymousFunctionOperation anonymousFunction,
        PluginAuthoringSymbols symbols)
    {
        foreach (var operation in anonymousFunction.Descendants())
        {
            ISymbol? referencedSymbol = operation switch
            {
                ILocalReferenceOperation localReference => localReference.Local,
                IParameterReferenceOperation parameterReference => parameterReference.Parameter,
                _ => null,
            };

            if (referencedSymbol is null)
            {
                continue;
            }

            var referencedType = referencedSymbol.GetSymbolType();
            if (!IsStartupConfigurationType(referencedType, symbols))
            {
                continue;
            }

            var isCaptured = !SymbolEqualityComparer.Default.Equals(
                referencedSymbol.ContainingSymbol,
                anonymousFunction.Symbol);

            if (isCaptured)
            {
                return true;
            }
        }

        return false;
    }

}
