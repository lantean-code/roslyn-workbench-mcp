using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Roslyn.Workbench.Mcp.ToolReferenceGenerator;

/// <summary>
/// Rejects production error emitters that bypass their assembly-owned stable-code constants.
/// </summary>
internal static class PublishedErrorCodeSourceValidator
{
    private static readonly string[] _sourceDirectories =
    [
        "Roslyn.Workbench.Mcp",
        "Roslyn.Workbench.Mcp.Workspace",
        "Roslyn.Workbench.Mcp.CodeActions",
        "Roslyn.Workbench.Mcp.Plugins",
        "Roslyn.Workbench.Mcp.Plugins.Core",
    ];

    private static readonly HashSet<string> _errorFactoryMethods = new(StringComparer.Ordinal)
    {
        "Conflict",
        "CreateFailure",
        "Faulted",
        "Rejected",
    };

    private static readonly HashSet<string> _allowedConstantOwnerNames = PublishedErrorCodeCatalogue.ConstantOwnerTypes
        .Select(static type => type.FullName
            ?? throw new InvalidOperationException($"Published error-code owner '{type.Name}' has no full name."))
        .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Verifies that production error-emission sites use constants from the closed published catalogue.
    /// </summary>
    /// <param name="repositoryRoot">The repository root containing the production projects.</param>
    public static void Validate(string repositoryRoot)
    {
        var syntaxTrees = GetProductionSourceFiles(repositoryRoot)
            .Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file))
            .ToArray();

        var compilation = CSharpCompilation.Create(
            "PublishedErrorCodeValidation",
            syntaxTrees,
            CreateFrameworkReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var violations = new List<string>();
        foreach (var syntaxTree in syntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            ValidateSyntaxTree(repositoryRoot, syntaxTree, semanticModel, violations);
        }

        if (violations.Count > 0)
        {
            throw new InvalidOperationException(
                "Published tool error codes must use assembly-owned constants: " + string.Join("; ", violations));
        }
    }

    private static PortableExecutableReference[] CreateFrameworkReferences()
    {
        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? throw new InvalidOperationException("Trusted platform assemblies were not available for error-code validation.");

        return trustedPlatformAssemblies.Split(Path.PathSeparator)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }

    private static IEnumerable<string> GetProductionSourceFiles(string repositoryRoot)
    {
        foreach (var sourceDirectory in _sourceDirectories)
        {
            var directory = Path.Combine(repositoryRoot, "src", sourceDirectory);
            foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                yield return file;
            }
        }
    }

    private static void ValidateSyntaxTree(
        string repositoryRoot,
        SyntaxTree syntaxTree,
        SemanticModel semanticModel,
        List<string> violations)
    {
        var root = syntaxTree.GetRoot();
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            ValidateInvocation(repositoryRoot, syntaxTree.FilePath, invocation, semanticModel, violations);
        }

        foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            ValidateErrorCodeAssignment(repositoryRoot, syntaxTree.FilePath, assignment, semanticModel, violations);
        }
    }

    private static void ValidateInvocation(
        string repositoryRoot,
        string file,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        List<string> violations)
    {
        var methodName = GetInvokedMethodName(invocation.Expression);
        if (_errorFactoryMethods.Contains(methodName))
        {
            var codeExpression = GetErrorCodeExpression(invocation, semanticModel);
            if (codeExpression is not null)
            {
                ValidateCodeExpression(repositoryRoot, file, codeExpression, semanticModel, violations);
            }

            return;
        }

        if (methodName != "WriteString")
        {
            return;
        }

        if (semanticModel.GetOperation(invocation) is not IInvocationOperation writeOperation)
        {
            return;
        }

        var propertyExpression = GetArgumentExpression(
            writeOperation,
            static parameter => parameter.Name.EndsWith("PropertyName", StringComparison.OrdinalIgnoreCase));

        var valueExpression = GetArgumentExpression(
            writeOperation,
            static parameter => parameter.Name == "value");

        if (propertyExpression is null || valueExpression is null)
        {
            return;
        }

        var propertyName = semanticModel.GetConstantValue(propertyExpression);
        if (!propertyName.HasValue || propertyName.Value is not "code")
        {
            return;
        }

        ValidateCodeExpression(repositoryRoot, file, valueExpression, semanticModel, violations);
    }

    private static void ValidateErrorCodeAssignment(
        string repositoryRoot,
        string file,
        AssignmentExpressionSyntax assignment,
        SemanticModel semanticModel,
        List<string> violations)
    {
        if (GetAssignedMemberName(assignment.Left) != "Code")
        {
            return;
        }

        var objectCreation = assignment.FirstAncestorOrSelf<ObjectCreationExpressionSyntax>();
        if (objectCreation is null
            || !objectCreation.Type.ToString().EndsWith("Error", StringComparison.Ordinal))
        {
            return;
        }

        ValidateCodeExpression(repositoryRoot, file, assignment.Right, semanticModel, violations);
    }

    private static void ValidateCodeExpression(
        string repositoryRoot,
        string file,
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        List<string> violations)
    {
        var constant = semanticModel.GetConstantValue(expression);
        if (!constant.HasValue
            || constant.Value is not string code
            || !IsCode(code))
        {
            return;
        }

        var symbol = semanticModel.GetSymbolInfo(expression).Symbol;
        if (symbol is IFieldSymbol { IsConst: true } field
            && field.ContainingType.ToDisplayString() is var ownerName
            && _allowedConstantOwnerNames.Contains(ownerName))
        {
            return;
        }

        AddViolation(repositoryRoot, file, expression, code, violations);
    }

    private static ExpressionSyntax? GetErrorCodeExpression(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        if (semanticModel.GetOperation(invocation) is IInvocationOperation invocationOperation)
        {
            return GetArgumentExpression(
                invocationOperation,
                static parameter => parameter.Type.SpecialType == SpecialType.System_String
                    && parameter.Name.EndsWith("Code", StringComparison.OrdinalIgnoreCase));
        }

        var arguments = invocation.ArgumentList.Arguments;
        var syntacticCodeArgument = arguments.FirstOrDefault(static argument =>
            argument.NameColon?.Name.Identifier.ValueText.EndsWith("Code", StringComparison.OrdinalIgnoreCase) == true);

        if (syntacticCodeArgument is not null)
        {
            return syntacticCodeArgument.Expression;
        }

        if (arguments.Count == 0 || arguments[0].NameColon is not null)
        {
            return null;
        }

        return arguments[0].Expression;
    }

    private static ExpressionSyntax? GetArgumentExpression(
        IInvocationOperation invocation,
        Func<IParameterSymbol, bool> parameterPredicate)
    {
        return invocation.Arguments
            .FirstOrDefault(argument => argument.Parameter is { } parameter && parameterPredicate(parameter))
            ?.Value.Syntax as ExpressionSyntax;
    }

    private static bool IsCode(string value)
    {
        return value.Length > 0
            && char.IsUpper(value[0])
            && value.All(static character => char.IsLetterOrDigit(character));
    }

    private static string GetInvokedMethodName(ExpressionSyntax expression)
    {
        return expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            GenericNameSyntax generic => generic.Identifier.ValueText,
            MemberAccessExpressionSyntax { Name: IdentifierNameSyntax identifier } => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax { Name: GenericNameSyntax generic } => generic.Identifier.ValueText,
            _ => string.Empty,
        };
    }

    private static string GetAssignedMemberName(ExpressionSyntax expression)
    {
        return expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            _ => string.Empty,
        };
    }

    private static void AddViolation(
        string repositoryRoot,
        string file,
        SyntaxNode node,
        string code,
        List<string> violations)
    {
        var relativePath = Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/');
        var line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        violations.Add($"{relativePath}:{line} emits uncatalogued constant '{code}'");
    }
}
