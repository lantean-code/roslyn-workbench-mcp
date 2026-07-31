using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Roslyn.Workbench.Mcp.Plugins.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PluginQueryCacheAnalyzer : DiagnosticAnalyzer
{
    private const string _cacheMetadataName = "Roslyn.Workbench.Mcp.Plugins.IQueryResultCache";
    private const string _disposableMetadataName = "System.IDisposable";
    private const string _asyncDisposableMetadataName = "System.IAsyncDisposable";
    private const string _pluginResultMetadataName = "Roslyn.Workbench.Mcp.Plugins.PluginExecutionResult`1";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(
            PluginDiagnosticDescriptors.InvalidQueryCacheKey,
            PluginDiagnosticDescriptors.UnsafeCachedValue);

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
        var cacheType = context.Compilation.GetTypeByMetadataName(_cacheMetadataName);
        var disposableType = context.Compilation.GetTypeByMetadataName(_disposableMetadataName);
        var asyncDisposableType = context.Compilation.GetTypeByMetadataName(_asyncDisposableMetadataName);
        var pluginResultType = context.Compilation.GetTypeByMetadataName(_pluginResultMetadataName);
        if (cacheType is null
            || disposableType is null
            || asyncDisposableType is null
            || pluginResultType is null)
        {
            return;
        }

        context.RegisterOperationAction(
            operationContext => AnalyzeInvocation(
                operationContext,
                cacheType,
                disposableType,
                asyncDisposableType,
                pluginResultType),
            OperationKind.Invocation);
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        INamedTypeSymbol cacheType,
        INamedTypeSymbol disposableType,
        INamedTypeSymbol asyncDisposableType,
        INamedTypeSymbol pluginResultType)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        if (method.TypeArguments.Length != 2
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, cacheType))
        {
            return;
        }

        var keyType = method.TypeArguments[0];
        var visitedKeyTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        if (!IsSafeKeyType(keyType, visitedKeyTypes))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                PluginDiagnosticDescriptors.InvalidQueryCacheKey,
                invocation.Arguments[0].Syntax.GetLocation(),
                keyType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
        }

        var valueType = method.TypeArguments[1];
        var visitedValueTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        if (IsUnsafeValueType(
            valueType,
            disposableType,
            asyncDisposableType,
            pluginResultType,
            visitedValueTypes))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                PluginDiagnosticDescriptors.UnsafeCachedValue,
                invocation.Syntax.GetLocation(),
                valueType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
        }
    }

    private static bool IsSafeKeyType(ITypeSymbol type, HashSet<ITypeSymbol> visited)
    {
        if (!visited.Add(type))
        {
            return true;
        }

        if (IsScalar(type))
        {
            return true;
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        if (namedType.IsValueType)
        {
            return AreRetainedMembersSafeForKey(namedType, visited);
        }

        if (type.TypeKind != TypeKind.Class
            || !namedType.IsSealed
            || namedType.SpecialType == SpecialType.System_Object
            || IsRoslynSnapshotType(namedType)
            || ImplementsMutableCollection(namedType)
            || !HasValueEquality(namedType))
        {
            return false;
        }

        return AreRetainedMembersSafeForKey(namedType, visited);
    }

    private static bool AreRetainedMembersSafeForKey(
        INamedTypeSymbol type,
        HashSet<ITypeSymbol> visited)
    {
        if (IsImmutableArray(type))
        {
            return type.TypeArguments.All(argument => IsSafeKeyType(argument, visited));
        }

        foreach (var member in GetRetainedMembers(type))
        {
            var memberType = GetRetainedMemberType(member);
            if (!IsReadOnly(member)
                || memberType is null
                || !IsSafeKeyType(memberType, visited))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsUnsafeValueType(
        ITypeSymbol type,
        INamedTypeSymbol disposableType,
        INamedTypeSymbol asyncDisposableType,
        INamedTypeSymbol pluginResultType,
        HashSet<ITypeSymbol> visited)
    {
        if (!visited.Add(type) || IsScalar(type))
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

        if (IsImmutableArray(namedType))
        {
            return namedType.TypeArguments.Any(argument => IsUnsafeValueType(
                argument,
                disposableType,
                asyncDisposableType,
                pluginResultType,
                visited));
        }

        if (PluginSymbolFacts.ImplementsInterface(namedType, disposableType)
            || PluginSymbolFacts.ImplementsInterface(namedType, asyncDisposableType)
            || SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, pluginResultType)
            || ImplementsMutableCollection(namedType))
        {
            return true;
        }

        foreach (var member in GetRetainedMembers(namedType))
        {
            var memberType = GetRetainedMemberType(member);
            if (!IsReadOnly(member)
                || memberType is null
                || IsUnsafeValueType(
                    memberType,
                    disposableType,
                    asyncDisposableType,
                    pluginResultType,
                    visited))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<ISymbol> GetRetainedMembers(INamedTypeSymbol type)
    {
        for (var current = type;
             current is not null && current.SpecialType != SpecialType.System_Object;
             current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                if (!member.IsStatic
                    && !member.IsImplicitlyDeclared
                    && !string.Equals(member.Name, "EqualityContract", StringComparison.Ordinal)
                    && (member is not IFieldSymbol field || field.AssociatedSymbol is null)
                    && !member.Name.EndsWith("k__BackingField", StringComparison.Ordinal)
                    && member is IFieldSymbol or IPropertySymbol)
                {
                    yield return member;
                }
            }
        }
    }

    private static bool IsReadOnly(ISymbol member)
    {
        return member switch
        {
            IFieldSymbol field => field.IsReadOnly,
            IPropertySymbol property => property.SetMethod is null
                || property.SetMethod.IsInitOnly
                || property.SetMethod.ReturnTypeCustomModifiers.Any(static modifier =>
                    string.Equals(
                        modifier.Modifier.ToDisplayString(),
                        "System.Runtime.CompilerServices.IsExternalInit",
                        StringComparison.Ordinal))
                || property.DeclaringSyntaxReferences.Any(static syntaxReference =>
                    syntaxReference.GetSyntax() is PropertyDeclarationSyntax propertyDeclaration
                    && propertyDeclaration.AccessorList?.Accessors.Any(static accessor =>
                        accessor.Keyword.IsKind(SyntaxKind.InitKeyword)) == true),
            _ => true,
        };
    }

    private static ITypeSymbol? GetRetainedMemberType(ISymbol member)
    {
        return member switch
        {
            IFieldSymbol field => field.Type,
            IPropertySymbol property => property.Type,
            _ => null,
        };
    }

    private static bool IsScalar(ITypeSymbol type)
    {
        return type.TypeKind == TypeKind.Enum
            || type.SpecialType is
                SpecialType.System_Boolean
                or SpecialType.System_Byte
                or SpecialType.System_SByte
                or SpecialType.System_Int16
                or SpecialType.System_UInt16
                or SpecialType.System_Int32
                or SpecialType.System_UInt32
                or SpecialType.System_Int64
                or SpecialType.System_UInt64
                or SpecialType.System_IntPtr
                or SpecialType.System_UIntPtr
                or SpecialType.System_Char
                or SpecialType.System_Double
                or SpecialType.System_Single
                or SpecialType.System_Decimal
                or SpecialType.System_String;
    }

    private static bool HasValueEquality(INamedTypeSymbol type)
    {
        if (type.IsRecord)
        {
            return true;
        }

        var overridesEquals = type.GetMembers(nameof(object.Equals))
            .OfType<IMethodSymbol>()
            .Any(static method => method.IsOverride);

        var overridesHashCode = type.GetMembers(nameof(object.GetHashCode))
            .OfType<IMethodSymbol>()
            .Any(static method => method.IsOverride);

        return overridesEquals && overridesHashCode;
    }

    private static bool ImplementsMutableCollection(INamedTypeSymbol type)
    {
        return type.AllInterfaces.Any(static contract =>
            contract.OriginalDefinition.SpecialType is SpecialType.System_Collections_Generic_ICollection_T
            || string.Equals(
                contract.OriginalDefinition.ToDisplayString(),
                "System.Collections.Generic.IDictionary<TKey, TValue>",
                StringComparison.Ordinal));
    }

    private static bool IsImmutableArray(INamedTypeSymbol type)
    {
        return string.Equals(
            type.OriginalDefinition.ToDisplayString(),
            "System.Collections.Immutable.ImmutableArray<T>",
            StringComparison.Ordinal);
    }

    private static bool IsRoslynSnapshotType(INamedTypeSymbol type)
    {
        return type.ContainingNamespace.ToDisplayString()
            .StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal);
    }
}
