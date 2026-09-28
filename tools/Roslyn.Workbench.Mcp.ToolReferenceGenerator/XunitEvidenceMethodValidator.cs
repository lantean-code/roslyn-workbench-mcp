using Microsoft.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.ToolReferenceGenerator;

/// <summary>
/// Validates whether a method symbol represents an executable, unconditional xUnit test.
/// </summary>
internal static class XunitEvidenceMethodValidator
{
    private const string _factAttribute = "Xunit.FactAttribute";
    private const string _theoryAttribute = "Xunit.TheoryAttribute";
    private const string _xunitCoreAssembly = "xunit.v3.core";

    /// <summary>
    /// Determines whether the supplied method can be discovered and executed as unconditional xUnit evidence.
    /// </summary>
    /// <param name="symbol">The method symbol to validate.</param>
    /// <param name="rejectionReason">The reason the method is not valid evidence, when validation fails.</param>
    /// <returns><see langword="true"/> when the method is executable unconditional evidence; otherwise, <see langword="false"/>.</returns>
    public static bool TryValidate(IMethodSymbol symbol, out string rejectionReason)
    {
        if (symbol.DeclaredAccessibility != Accessibility.Public
            || symbol.IsAbstract
            || symbol.IsGenericMethod
            || symbol.IsStatic)
        {
            rejectionReason = "the method must be public, concrete, non-generic and non-static";
            return false;
        }

        if (!IsDiscoverableContainingType(symbol.ContainingType))
        {
            rejectionReason = "the containing type and its parents must be public, concrete and non-generic";
            return false;
        }

        var testAttributes = symbol.GetAttributes()
            .Where(static attribute => attribute.AttributeClass is { } attributeClass
                && GetQualifiedMetadataName(attributeClass) is _factAttribute or _theoryAttribute
                && attributeClass.ContainingAssembly.Identity.Name == _xunitCoreAssembly)
            .ToArray();

        if (testAttributes.Length != 1)
        {
            var attributeNames = symbol.GetAttributes().Select(GetAttributeName);
            rejectionReason = $"the resolved attributes were {string.Join(", ", attributeNames)}";
            return false;
        }

        var testAttribute = testAttributes[0];
        var conditionalArgument = testAttribute.NamedArguments.FirstOrDefault(static argument =>
            argument.Key == "Explicit"
            || argument.Key.StartsWith("Skip", StringComparison.Ordinal));

        if (!string.IsNullOrEmpty(conditionalArgument.Key))
        {
            rejectionReason = $"the attribute sets '{conditionalArgument.Key}'";
            return false;
        }

        if (!HasSupportedReturnType(symbol))
        {
            rejectionReason = "the method must return void, Task, Task<T>, ValueTask or ValueTask<T>";
            return false;
        }

        if (symbol.Parameters.Any(static parameter => parameter.RefKind != RefKind.None))
        {
            rejectionReason = "test parameters must be passed by value";
            return false;
        }

        var attributeName = GetAttributeName(testAttribute);
        if (attributeName == _theoryAttribute)
        {
            rejectionReason = "Theory methods are not accepted as security evidence; cite an unconditional Fact";
            return false;
        }

        if (symbol.Parameters.Length != 0)
        {
            rejectionReason = "Fact methods must not declare parameters";
            return false;
        }

        rejectionReason = string.Empty;
        return true;
    }

    private static bool IsDiscoverableContainingType(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility != Accessibility.Public
                || current.IsAbstract
                || current.IsGenericType)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasSupportedReturnType(IMethodSymbol symbol)
    {
        if (symbol.ReturnsVoid)
        {
            return !symbol.IsAsync;
        }

        var returnType = symbol.ReturnType.OriginalDefinition;
        if (returnType.ContainingNamespace.ToDisplayString() != "System.Threading.Tasks")
        {
            return false;
        }

        return returnType.MetadataName is "Task" or "Task`1" or "ValueTask" or "ValueTask`1";
    }

    private static string GetQualifiedMetadataName(INamedTypeSymbol type)
    {
        return $"{type.ContainingNamespace.ToDisplayString()}.{type.MetadataName}";
    }

    private static string GetAttributeName(AttributeData attribute)
    {
        if (attribute.AttributeClass is not { } attributeClass)
        {
            return "<unresolved>";
        }

        return GetQualifiedMetadataName(attributeClass);
    }
}
