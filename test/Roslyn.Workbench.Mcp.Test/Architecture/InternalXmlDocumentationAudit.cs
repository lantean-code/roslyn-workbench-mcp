using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Roslyn.Workbench.Mcp.Test.Architecture;

internal static class InternalXmlDocumentationAudit
{
    private static readonly SyntaxTree _implicitUsings = CSharpSyntaxTree.ParseText("""
        global using System;
        global using System.Collections.Generic;
        global using System.IO;
        global using System.Linq;
        global using System.Net.Http;
        global using System.Threading;
        global using System.Threading.Tasks;
        """);
    private static readonly Lazy<MetadataReference[]> _platformReferences = new(CreatePlatformReferences);
    private static readonly Lazy<DocumentationCompilation> _productionDocumentationCompilation = new(CreateProductionDocumentationCompilation);

    private static readonly SyntaxKind[] _governedAccessibilities =
    [
        SyntaxKind.PublicKeyword,
        SyntaxKind.InternalKeyword,
        SyntaxKind.ProtectedKeyword,
    ];

    public static IReadOnlyList<InternalXmlDocumentationFinding> FindUndocumentedDeclarations(IEnumerable<string> paths)
    {
        var declarations = paths
            .SelectMany(ParseGovernedDeclarations)
            .GroupBy(static declaration => declaration.Key, StringComparer.Ordinal)
            .Select(static declarations => CreateFinding(declarations))
            .OfType<InternalXmlDocumentationFinding>()
            .OrderBy(static finding => finding.Key, StringComparer.Ordinal)
            .ToArray();

        return declarations;
    }

    public static IReadOnlyList<InternalXmlDocumentationFinding> FindUndocumentedDeclarations(string source, string path)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, path: path);
        return EnumerateGovernedDeclarations(syntaxTree, path)
            .GroupBy(static declaration => declaration.Key, StringComparer.Ordinal)
            .Select(static declarations => CreateFinding(declarations))
            .OfType<InternalXmlDocumentationFinding>()
            .OrderBy(static finding => finding.Key, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<InternalXmlDocumentationQualityFinding> FindDocumentationQualityIssues(IEnumerable<string> paths)
    {
        var analysis = _productionDocumentationCompilation.Value;
        return paths
            .Select(path => analysis.GetSyntaxTree(path))
            .SelectMany(syntaxTree => EnumerateGovernedDeclarations(
                syntaxTree,
                syntaxTree.FilePath,
                analysis.Compilation.GetSemanticModel(syntaxTree)))
            .GroupBy(static declaration => declaration.Key, StringComparer.Ordinal)
            .SelectMany(InspectDocumentationQuality)
            .OrderBy(static finding => finding.Key, StringComparer.Ordinal)
            .ThenBy(static finding => finding.Rule)
            .ToArray();
    }

    public static IReadOnlyList<InternalXmlDocumentationQualityFinding> FindDocumentationQualityIssues(string source, string path)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, path: path);
        var compilation = CSharpCompilation.Create(
            "InternalXmlDocumentationAudit",
            [_implicitUsings, syntaxTree],
            _platformReferences.Value);
        return EnumerateGovernedDeclarations(syntaxTree, path, compilation.GetSemanticModel(syntaxTree))
            .GroupBy(static declaration => declaration.Key, StringComparer.Ordinal)
            .SelectMany(InspectDocumentationQuality)
            .OrderBy(static finding => finding.Key, StringComparer.Ordinal)
            .ThenBy(static finding => finding.Rule)
            .ToArray();
    }

    private static InternalXmlDocumentationFinding? CreateFinding(IEnumerable<GovernedDeclaration> declarations)
    {
        var declarationList = declarations.ToArray();
        if (declarationList.Any(static declaration => declaration.Documentation is not null))
        {
            return null;
        }

        var firstDeclaration = declarationList
            .OrderBy(static declaration => declaration.Path, StringComparer.Ordinal)
            .ThenBy(static declaration => declaration.Line)
            .First();

        return new InternalXmlDocumentationFinding(
            firstDeclaration.Key,
            firstDeclaration.Path,
            firstDeclaration.Line);
    }

    private static IEnumerable<GovernedDeclaration> ParseGovernedDeclarations(string path)
    {
        var source = File.ReadAllText(path);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, path: path);
        return EnumerateGovernedDeclarations(syntaxTree, path);
    }

    private static DocumentationCompilation CreateProductionDocumentationCompilation()
    {
        var syntaxTrees = ProductionSourceAudit
            .EnumerateSourceFiles()
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: Path.GetFullPath(path)))
            .ToArray();
        var compilation = CSharpCompilation.Create(
            "InternalXmlDocumentationAudit",
            syntaxTrees.Prepend(_implicitUsings),
            _platformReferences.Value);
        return new DocumentationCompilation(compilation, syntaxTrees);
    }

    private static MetadataReference[] CreatePlatformReferences()
    {
        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? throw new InvalidOperationException("The test host did not provide its trusted platform assembly list.");
        return trustedPlatformAssemblies
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }

    private static IEnumerable<GovernedDeclaration> EnumerateGovernedDeclarations(
        SyntaxTree syntaxTree,
        string path,
        SemanticModel? semanticModel = null)
    {
        var root = syntaxTree.GetRoot();
        var projectName = GetProjectName(path);
        foreach (var declaration in root.DescendantNodes().OfType<MemberDeclarationSyntax>())
        {
            if (!IsSupportedDeclaration(declaration) || !IsGoverned(declaration))
            {
                continue;
            }

            var key = CreateKey(projectName, declaration);
            var line = declaration.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            var relativePath = Path.GetRelativePath(ProductionSourceAudit.RepositoryRoot, path);
            yield return new GovernedDeclaration(
                key,
                relativePath,
                line,
                declaration,
                GetDocumentation(declaration),
                semanticModel);
        }
    }

    private static IEnumerable<InternalXmlDocumentationQualityFinding> InspectDocumentationQuality(IEnumerable<GovernedDeclaration> declarations)
    {
        foreach (var declaration in declarations.Where(static item => item.Documentation is not null))
        {
            var documentation = declaration.Documentation!;
            if (HasInheritdoc(documentation))
            {
                if (!CanInheritDocumentation(documentation, declaration.Declaration, declaration.SemanticModel!))
                {
                    yield return CreateQualityFinding(declaration, InternalXmlDocumentationQualityRule.Inheritdoc, "Use inheritdoc only when the declaration inherits or implements a documented contract.");
                }

                continue;
            }

            var summary = FindElement(documentation, "summary");
            if (summary is not null && IsSingleLine(summary))
            {
                yield return CreateQualityFinding(declaration, InternalXmlDocumentationQualityRule.SummaryLayout, "Place the summary start tag, content, and end tag on separate lines.");
            }

            if (summary is not null
                && declaration.Declaration is ConstructorDeclarationSyntax
                && !HasStandardConstructorSummary(summary, declaration.Declaration))
            {
                yield return CreateQualityFinding(declaration, InternalXmlDocumentationQualityRule.SummaryContent, "Use the standard 'Initializes a new instance of the type class or structure.' constructor summary with the correct type kind.");
            }
            else if (summary is not null && HasGeneratedPlaceholderText(summary, declaration.Declaration))
            {
                yield return CreateQualityFinding(declaration, InternalXmlDocumentationQualityRule.SummaryContent, "Replace generated placeholder wording with the declaration's actual responsibility or result.");
            }

            foreach (var parameterName in GetParameterNames(declaration.Declaration))
            {
                if (!HasNamedElement(documentation, "param", parameterName))
                {
                    yield return CreateQualityFinding(declaration, InternalXmlDocumentationQualityRule.Parameter, $"Document parameter '{parameterName}'.");
                }
                else if (HasGeneratedParameterDescription(documentation, parameterName, declaration.Declaration))
                {
                    yield return CreateQualityFinding(declaration, InternalXmlDocumentationQualityRule.ParameterContent, $"Explain how parameter '{parameterName}' is used by the declaration.");
                }
            }

            foreach (var typeParameterName in GetTypeParameterNames(declaration.Declaration))
            {
                if (!HasNamedElement(documentation, "typeparam", typeParameterName))
                {
                    yield return CreateQualityFinding(declaration, InternalXmlDocumentationQualityRule.TypeParameter, $"Document type parameter '{typeParameterName}'.");
                }
            }

            if (RequiresReturnsDocumentation(declaration.Declaration)
                && !HasContentElement(documentation, "returns"))
            {
                yield return CreateQualityFinding(declaration, InternalXmlDocumentationQualityRule.Returns, "Document the returned value.");
            }
            else if (RequiresReturnsDocumentation(declaration.Declaration)
                && HasGeneratedReturnsText(documentation))
            {
                yield return CreateQualityFinding(declaration, InternalXmlDocumentationQualityRule.ReturnsContent, "Explain the meaning of the returned value instead of restating its type.");
            }
        }
    }

    private static InternalXmlDocumentationQualityFinding CreateQualityFinding(GovernedDeclaration declaration, InternalXmlDocumentationQualityRule rule, string message)
    {
        return new InternalXmlDocumentationQualityFinding(
            declaration.Key,
            declaration.Path,
            declaration.Line,
            rule,
            message);
    }

    private static bool IsSupportedDeclaration(MemberDeclarationSyntax declaration)
    {
        return declaration is BaseTypeDeclarationSyntax
            or DelegateDeclarationSyntax
            or MethodDeclarationSyntax
            or ConstructorDeclarationSyntax
            or OperatorDeclarationSyntax
            or ConversionOperatorDeclarationSyntax
            or PropertyDeclarationSyntax
            or IndexerDeclarationSyntax
            or EventDeclarationSyntax
            or EventFieldDeclarationSyntax
            or FieldDeclarationSyntax
            or EnumMemberDeclarationSyntax;
    }

    private static bool IsGoverned(MemberDeclarationSyntax declaration)
    {
        if (IsInsideExcludedType(declaration))
        {
            return false;
        }

        if (declaration is BaseTypeDeclarationSyntax typeDeclaration)
        {
            return IsGovernedType(typeDeclaration);
        }

        if (declaration is DelegateDeclarationSyntax delegateDeclaration)
        {
            return IsGovernedDeclaration(delegateDeclaration.Modifiers, delegateDeclaration.Parent);
        }

        if (HasExplicitInterfaceSpecifier(declaration))
        {
            return false;
        }

        var modifiers = GetModifiers(declaration);
        return IsGovernedDeclaration(modifiers, declaration.Parent);
    }

    private static bool HasExplicitInterfaceSpecifier(MemberDeclarationSyntax declaration)
    {
        return declaration switch
        {
            MethodDeclarationSyntax method => method.ExplicitInterfaceSpecifier is not null,
            PropertyDeclarationSyntax property => property.ExplicitInterfaceSpecifier is not null,
            IndexerDeclarationSyntax indexer => indexer.ExplicitInterfaceSpecifier is not null,
            EventDeclarationSyntax @event => @event.ExplicitInterfaceSpecifier is not null,
            _ => false,
        };
    }

    private static bool IsInsideExcludedType(MemberDeclarationSyntax declaration)
    {
        return declaration
            .Ancestors()
            .OfType<BaseTypeDeclarationSyntax>()
            .Any(static type => type.Modifiers.Any(SyntaxKind.FileKeyword)
                || IsPrivateOnly(type.Modifiers)
                || IsImplicitlyPrivateNestedType(type));
    }

    private static bool IsGovernedType(BaseTypeDeclarationSyntax declaration)
    {
        if (declaration.Modifiers.Any(SyntaxKind.FileKeyword) || IsPrivateOnly(declaration.Modifiers))
        {
            return false;
        }

        return IsGovernedDeclaration(declaration.Modifiers, declaration.Parent);
    }

    private static bool IsGovernedDeclaration(SyntaxTokenList modifiers, SyntaxNode? parent)
    {
        if (IsPrivateOnly(modifiers))
        {
            return false;
        }

        if (modifiers.Any(static modifier => _governedAccessibilities.Contains(modifier.Kind())))
        {
            return true;
        }

        if (parent is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax)
        {
            return true;
        }

        return parent is InterfaceDeclarationSyntax or EnumDeclarationSyntax;
    }

    private static bool IsPrivateOnly(SyntaxTokenList modifiers)
    {
        return modifiers.Any(SyntaxKind.PrivateKeyword)
            && !modifiers.Any(SyntaxKind.ProtectedKeyword);
    }

    private static bool IsImplicitlyPrivateNestedType(BaseTypeDeclarationSyntax declaration)
    {
        return declaration.Parent is BaseTypeDeclarationSyntax and not InterfaceDeclarationSyntax
            && !declaration.Modifiers.Any(static modifier => _governedAccessibilities.Contains(modifier.Kind()));
    }

    private static SyntaxTokenList GetModifiers(MemberDeclarationSyntax declaration)
    {
        if (declaration is MethodDeclarationSyntax method)
        {
            return method.Modifiers;
        }

        if (declaration is ConstructorDeclarationSyntax constructor)
        {
            return constructor.Modifiers;
        }

        if (declaration is OperatorDeclarationSyntax @operator)
        {
            return @operator.Modifiers;
        }

        if (declaration is ConversionOperatorDeclarationSyntax conversion)
        {
            return conversion.Modifiers;
        }

        if (declaration is PropertyDeclarationSyntax property)
        {
            return property.Modifiers;
        }

        if (declaration is IndexerDeclarationSyntax indexer)
        {
            return indexer.Modifiers;
        }

        if (declaration is EventDeclarationSyntax @event)
        {
            return @event.Modifiers;
        }

        if (declaration is EventFieldDeclarationSyntax eventField)
        {
            return eventField.Modifiers;
        }

        if (declaration is FieldDeclarationSyntax field)
        {
            return field.Modifiers;
        }

        return default;
    }

    private static DocumentationCommentTriviaSyntax? GetDocumentation(MemberDeclarationSyntax declaration)
    {
        foreach (var trivia in declaration.GetLeadingTrivia())
        {
            if (trivia.GetStructure() is not DocumentationCommentTriviaSyntax documentation)
            {
                continue;
            }

            foreach (var content in documentation.Content)
            {
                if (content is XmlElementSyntax element && IsDocumentingElement(element))
                {
                    return documentation;
                }

                if (content is XmlEmptyElementSyntax emptyElement
                    && emptyElement.Name.LocalName.ValueText == "inheritdoc")
                {
                    return documentation;
                }
            }
        }

        return null;
    }

    private static bool HasInheritdoc(DocumentationCommentTriviaSyntax documentation)
    {
        return documentation.Content.Any(static content =>
            content is XmlEmptyElementSyntax emptyElement
                && emptyElement.Name.LocalName.ValueText == "inheritdoc"
            || content is XmlElementSyntax element
                && element.StartTag.Name.LocalName.ValueText == "inheritdoc");
    }

    private static XmlElementSyntax? FindElement(DocumentationCommentTriviaSyntax documentation, string elementName)
    {
        return documentation.Content
            .OfType<XmlElementSyntax>()
            .FirstOrDefault(element => element.StartTag.Name.LocalName.ValueText == elementName);
    }

    private static bool HasContentElement(DocumentationCommentTriviaSyntax documentation, string elementName)
    {
        var element = FindElement(documentation, elementName);
        return element is not null && HasSummaryContent(element);
    }

    private static bool HasNamedElement(DocumentationCommentTriviaSyntax documentation, string elementName, string expectedName)
    {
        return documentation.Content
            .OfType<XmlElementSyntax>()
            .Where(element => element.StartTag.Name.LocalName.ValueText == elementName)
            .Any(element => HasElementName(element, expectedName) && HasSummaryContent(element));
    }

    private static bool HasGeneratedParameterDescription(
        DocumentationCommentTriviaSyntax documentation,
        string parameterName,
        MemberDeclarationSyntax declaration)
    {
        if (parameterName == "cancellationToken")
        {
            return false;
        }

        var parameter = documentation.Content
            .OfType<XmlElementSyntax>()
            .Where(static element => element.StartTag.Name.LocalName.ValueText == "param")
            .FirstOrDefault(element => HasElementName(element, parameterName));

        if (parameter is null)
        {
            return false;
        }

        var description = GetElementText(parameter);
        var words = SplitIdentifier(parameterName);
        return description.Equals($"The {words}.", StringComparison.OrdinalIgnoreCase)
            || description.Equals($"A {words}.", StringComparison.OrdinalIgnoreCase)
            || description.Equals($"An {words}.", StringComparison.OrdinalIgnoreCase)
            || declaration is not ConstructorDeclarationSyntax
                && description.EndsWith(" retained by the new instance.", StringComparison.Ordinal);
    }

    private static bool HasElementName(XmlElementSyntax element, string expectedName)
    {
        return element.StartTag.Attributes
            .OfType<XmlNameAttributeSyntax>()
            .Any(attribute => attribute.Name.LocalName.ValueText == "name"
                && attribute.Identifier.Identifier.ValueText == expectedName);
    }

    private static bool IsSingleLine(XmlElementSyntax element)
    {
        var lineSpan = element.GetLocation().GetLineSpan();
        return lineSpan.StartLinePosition.Line == lineSpan.EndLinePosition.Line;
    }

    private static IEnumerable<string> GetParameterNames(MemberDeclarationSyntax declaration)
    {
        BaseParameterListSyntax? parameters = declaration switch
        {
            MethodDeclarationSyntax method => method.ParameterList,
            ConstructorDeclarationSyntax constructor => constructor.ParameterList,
            OperatorDeclarationSyntax @operator => @operator.ParameterList,
            ConversionOperatorDeclarationSyntax conversion => conversion.ParameterList,
            IndexerDeclarationSyntax indexer => indexer.ParameterList,
            DelegateDeclarationSyntax @delegate => @delegate.ParameterList,
            _ => null,
        };

        return parameters?.Parameters.Select(static parameter => parameter.Identifier.ValueText)
            ?? [];
    }

    private static IEnumerable<string> GetTypeParameterNames(MemberDeclarationSyntax declaration)
    {
        TypeParameterListSyntax? typeParameters = declaration switch
        {
            TypeDeclarationSyntax type => type.TypeParameterList,
            MethodDeclarationSyntax method => method.TypeParameterList,
            DelegateDeclarationSyntax @delegate => @delegate.TypeParameterList,
            _ => null,
        };

        return typeParameters?.Parameters.Select(static parameter => parameter.Identifier.ValueText)
            ?? [];
    }

    private static bool RequiresReturnsDocumentation(MemberDeclarationSyntax declaration)
    {
        if (declaration is MethodDeclarationSyntax method)
        {
            return method.ReturnType is not PredefinedTypeSyntax predefinedType
                || !predefinedType.Keyword.IsKind(SyntaxKind.VoidKeyword);
        }

        if (declaration is DelegateDeclarationSyntax @delegate)
        {
            return @delegate.ReturnType is not PredefinedTypeSyntax predefinedType
                || !predefinedType.Keyword.IsKind(SyntaxKind.VoidKeyword);
        }

        return declaration is OperatorDeclarationSyntax
            or ConversionOperatorDeclarationSyntax;
    }

    private static bool IsDocumentingElement(XmlElementSyntax element)
    {
        var elementName = element.StartTag.Name.LocalName.ValueText;
        if (elementName == "inheritdoc")
        {
            return true;
        }

        return elementName == "summary" && HasSummaryContent(element);
    }

    private static bool HasSummaryContent(XmlElementSyntax element)
    {
        foreach (var content in element.Content)
        {
            if (content is not XmlTextSyntax text)
            {
                return true;
            }

            foreach (var token in text.TextTokens)
            {
                if (!string.IsNullOrWhiteSpace(token.ValueText))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasGeneratedPlaceholderText(XmlElementSyntax summary, MemberDeclarationSyntax declaration)
    {
        var text = GetElementText(summary);

        return text.StartsWith("Performs the ", StringComparison.Ordinal)
                && text.EndsWith(" operation.", StringComparison.Ordinal)
            || text.EndsWith(" associated value.", StringComparison.Ordinal)
            || text.Contains("created .", StringComparison.Ordinal)
            || text is "Creates d." or "Resolves d." or "Acquires d." or "Updates d." or "Gets id."
            || text is "Creates enabled." or "Creates disabled." or "Creates structured." or "Registers all."
            || text is "Gets the empty." or "Gets the unavailable." or "Gets the succeeded." or "Gets the failed."
            || text is "Gets the or create."
            || text.StartsWith("Gets a value indicating whether is ", StringComparison.Ordinal)
            || text.StartsWith("Gets a value indicating whether has ", StringComparison.Ordinal)
            || text.StartsWith("Gets a value indicating whether can ", StringComparison.Ordinal)
            || text.StartsWith("Gets a value indicating whether candidate", StringComparison.Ordinal)
            || text.Contains("used when performing the operation", StringComparison.Ordinal)
            || text.Contains("used when initializing the new instance", StringComparison.Ordinal)
            || RestatesContainingType(text, declaration);
    }

    private static bool HasStandardConstructorSummary(XmlElementSyntax summary, MemberDeclarationSyntax declaration)
    {
        var containingType = declaration.Ancestors().OfType<TypeDeclarationSyntax>().First();
        var typeKind = containingType is StructDeclarationSyntax
            || containingType is RecordDeclarationSyntax record
                && record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword)
            ? "structure"
            : "class";
        var expectedCref = containingType.Identifier.ValueText;
        if (containingType.TypeParameterList is { Parameters.Count: > 0 } typeParameters)
        {
            var parameterNames = string.Join(",", typeParameters.Parameters.Select(static parameter => parameter.Identifier.ValueText));
            expectedCref += $"{{{parameterNames}}}";
        }

        var content = summary.Content
            .Where(static node => node is not XmlTextSyntax text || !string.IsNullOrWhiteSpace(GetXmlText(text)))
            .ToArray();
        if (content is not
            [XmlTextSyntax prefix, XmlEmptyElementSyntax seeElement, XmlTextSyntax suffix]
            || seeElement.Name.LocalName.ValueText != "see"
            || !string.Equals(GetXmlText(prefix).Trim(), "Initializes a new instance of the", StringComparison.Ordinal)
            || !string.Equals(GetXmlText(suffix).Trim(), $"{typeKind}.", StringComparison.Ordinal))
        {
            return false;
        }

        var crefAttributes = seeElement.Attributes
            .OfType<XmlCrefAttributeSyntax>()
            .Take(2)
            .ToArray();
        return crefAttributes.Length == 1
            && string.Equals(RemoveWhitespace(crefAttributes[0].Cref.ToString()), expectedCref, StringComparison.Ordinal);
    }

    private static string RemoveWhitespace(string value)
    {
        return string.Concat(value.Where(static character => !char.IsWhiteSpace(character)));
    }

    private static bool RestatesContainingType(string summary, MemberDeclarationSyntax declaration)
    {
        var containingType = declaration.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault();
        if (containingType is null)
        {
            return false;
        }

        var typeWords = SplitIdentifier(containingType.Identifier.ValueText);
        string[] verbs =
        [
            "Activates",
            "Appends",
            "Composes",
            "Configures",
            "Dispatches",
            "Discovers",
            "Evaluates",
            "Inspects",
            "Maps",
            "Prepares",
            "Publishes",
            "Serializes",
            "Stages",
            "Starts",
            "Stops",
            "Transforms",
            "Updates",
            "Validates",
        ];

        return verbs.Any(verb => summary.Equals($"{verb} the {typeWords}.", StringComparison.OrdinalIgnoreCase)
            || summary.Equals($"{verb} a {typeWords}.", StringComparison.OrdinalIgnoreCase)
            || summary.Equals($"{verb} an {typeWords}.", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasGeneratedReturnsText(DocumentationCommentTriviaSyntax documentation)
    {
        var returns = FindElement(documentation, "returns");
        if (returns is null)
        {
            return false;
        }

        var text = GetElementText(returns);
        return text is "The bool."
            or "The byte array."
            or "The int."
            or "The string."
            or "The read only list."
            or "The immutable array."
            or "The t value?."
            or "The resolved d."
            or "The acquired d."
            or "A task that completes with the bool."
            or "A task that completes with the byte array."
            or "A task that completes with the int."
            or "A task that completes with the string."
            or "A task that completes with the read only list."
            or "A task that completes with the immutable array."
            or "A task that completes with the t value?.";
    }

    private static bool CanInheritDocumentation(
        DocumentationCommentTriviaSyntax documentation,
        MemberDeclarationSyntax declaration,
        SemanticModel semanticModel)
    {
        if (HasInheritdocCref(documentation))
        {
            return true;
        }

        var modifiers = GetModifiers(declaration);
        if (modifiers.Any(SyntaxKind.OverrideKeyword) || HasExplicitInterfaceSpecifier(declaration))
        {
            return true;
        }

        if (declaration is BaseTypeDeclarationSyntax typeDeclaration)
        {
            return typeDeclaration.BaseList is { Types.Count: > 0 };
        }

        if (modifiers.Any(SyntaxKind.StaticKeyword))
        {
            return false;
        }

        var declaredSymbol = semanticModel.GetDeclaredSymbol(declaration);
        if (declaredSymbol?.ContainingType is not { } containingType)
        {
            return false;
        }

        return containingType.AllInterfaces
            .SelectMany(static interfaceType => interfaceType.GetMembers())
            .Any(interfaceMember => SymbolEqualityComparer.Default.Equals(
                containingType.FindImplementationForInterfaceMember(interfaceMember),
                declaredSymbol));
    }

    private static bool HasInheritdocCref(DocumentationCommentTriviaSyntax documentation)
    {
        return documentation.Content.Any(static content => content switch
        {
            XmlEmptyElementSyntax emptyElement when emptyElement.Name.LocalName.ValueText == "inheritdoc" =>
                emptyElement.Attributes.OfType<XmlCrefAttributeSyntax>().Any(),
            XmlElementSyntax element when element.StartTag.Name.LocalName.ValueText == "inheritdoc" =>
                element.StartTag.Attributes.OfType<XmlCrefAttributeSyntax>().Any(),
            _ => false,
        });
    }

    private static string GetElementText(XmlElementSyntax element)
    {
        return string.Concat(element.Content
            .OfType<XmlTextSyntax>()
            .SelectMany(static content => content.TextTokens)
            .Select(static token => token.ValueText))
            .Trim();
    }

    private static string GetXmlText(XmlTextSyntax text)
    {
        return string.Concat(text.TextTokens.Select(static token => token.ValueText));
    }

    private static string SplitIdentifier(string identifier)
    {
        var characters = new List<char>(identifier.Length + 8);
        for (var index = 0; index < identifier.Length; index++)
        {
            var current = identifier[index];
            if (index > 0
                && char.IsUpper(current)
                && (char.IsLower(identifier[index - 1])
                    || index + 1 < identifier.Length && char.IsLower(identifier[index + 1])))
            {
                characters.Add(' ');
            }

            characters.Add(char.ToLowerInvariant(current));
        }

        return new string([.. characters]);
    }

    private static string GetProjectName(string path)
    {
        var relativePath = Path.IsPathRooted(path)
            ? Path.GetRelativePath(ProductionSourceAudit.RepositoryRoot, path)
            : path;
        var segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var sourceIndex = Array.IndexOf(segments, "src");
        return segments[sourceIndex + 1];
    }

    private static string CreateKey(string projectName, MemberDeclarationSyntax declaration)
    {
        var namespaceName = string.Join(
            ".",
            declaration.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse().Select(static item => item.Name.ToString()));

        var containingTypes = declaration
            .Ancestors()
            .OfType<BaseTypeDeclarationSyntax>()
            .Reverse()
            .Select(GetTypeName);

        var owner = string.Join(".", containingTypes.Prepend(namespaceName).Where(static item => item.Length > 0));
        var declarationName = GetDeclarationName(declaration);
        return $"{projectName}|{owner}|{declarationName}";
    }

    private static string GetDeclarationName(MemberDeclarationSyntax declaration)
    {
        if (declaration is BaseTypeDeclarationSyntax type)
        {
            return $"type {GetTypeName(type)}";
        }

        if (declaration is DelegateDeclarationSyntax @delegate)
        {
            return $"delegate {GetName(@delegate.Identifier, @delegate.TypeParameterList)}{GetParameters(@delegate.ParameterList)}";
        }

        if (declaration is MethodDeclarationSyntax method)
        {
            return $"method {GetName(method.Identifier, method.TypeParameterList)}{GetParameters(method.ParameterList)}";
        }

        if (declaration is ConstructorDeclarationSyntax constructor)
        {
            return $"constructor {constructor.Identifier.ValueText}{GetParameters(constructor.ParameterList)}";
        }

        if (declaration is OperatorDeclarationSyntax @operator)
        {
            return $"operator {@operator.OperatorToken.ValueText}{GetParameters(@operator.ParameterList)}";
        }

        if (declaration is ConversionOperatorDeclarationSyntax conversion)
        {
            return $"conversion {conversion.ImplicitOrExplicitKeyword.ValueText} {conversion.Type}{GetParameters(conversion.ParameterList)}";
        }

        if (declaration is PropertyDeclarationSyntax property)
        {
            return $"property {property.Identifier.ValueText}";
        }

        if (declaration is IndexerDeclarationSyntax indexer)
        {
            return $"indexer{GetParameters(indexer.ParameterList)}";
        }

        if (declaration is EventDeclarationSyntax @event)
        {
            return $"event {@event.Identifier.ValueText}";
        }

        if (declaration is EventFieldDeclarationSyntax eventField)
        {
            return $"event {string.Join(",", eventField.Declaration.Variables.Select(static variable => variable.Identifier.ValueText))}";
        }

        if (declaration is FieldDeclarationSyntax field)
        {
            return $"field {string.Join(",", field.Declaration.Variables.Select(static variable => variable.Identifier.ValueText))}";
        }

        var enumMember = (EnumMemberDeclarationSyntax)declaration;
        return $"enum member {enumMember.Identifier.ValueText}";
    }

    private static string GetTypeName(BaseTypeDeclarationSyntax declaration)
    {
        if (declaration is TypeDeclarationSyntax type)
        {
            return GetName(type.Identifier, type.TypeParameterList);
        }

        var enumDeclaration = (EnumDeclarationSyntax)declaration;
        return enumDeclaration.Identifier.ValueText;
    }

    private static string GetName(SyntaxToken identifier, TypeParameterListSyntax? typeParameters)
    {
        var arity = typeParameters?.Parameters.Count ?? 0;
        return arity == 0 ? identifier.ValueText : $"{identifier.ValueText}`{arity}";
    }

    private static string GetParameters(BaseParameterListSyntax parameters)
    {
        var parameterTypes = parameters.Parameters.Select(static parameter =>
        {
            var modifiers = string.Join(" ", parameter.Modifiers.Select(static modifier => modifier.ValueText));
            var type = parameter.Type?.ToString() ?? "unknown";
            return modifiers.Length == 0 ? type : $"{modifiers} {type}";
        });

        return $"({string.Join(",", parameterTypes)})";
    }

    private sealed class DocumentationCompilation
    {
        private readonly Dictionary<string, SyntaxTree> _syntaxTrees;

        public DocumentationCompilation(CSharpCompilation compilation, IEnumerable<SyntaxTree> syntaxTrees)
        {
            Compilation = compilation;
            _syntaxTrees = syntaxTrees.ToDictionary(static tree => tree.FilePath, StringComparer.Ordinal);
        }

        public CSharpCompilation Compilation { get; }

        public SyntaxTree GetSyntaxTree(string path)
        {
            return _syntaxTrees[Path.GetFullPath(path)];
        }
    }

    private sealed class GovernedDeclaration
    {
        public GovernedDeclaration(
            string key,
            string path,
            int line,
            MemberDeclarationSyntax declaration,
            DocumentationCommentTriviaSyntax? documentation,
            SemanticModel? semanticModel)
        {
            Key = key;
            Path = path;
            Line = line;
            Declaration = declaration;
            Documentation = documentation;
            SemanticModel = semanticModel;
        }

        public string Key { get; }

        public string Path { get; }

        public int Line { get; }

        public MemberDeclarationSyntax Declaration { get; }

        public DocumentationCommentTriviaSyntax? Documentation { get; }

        public SemanticModel? SemanticModel { get; }
    }
}

internal enum InternalXmlDocumentationQualityRule
{
    SummaryLayout,
    SummaryContent,
    Inheritdoc,
    Parameter,
    ParameterContent,
    TypeParameter,
    Returns,
    ReturnsContent,
}

internal sealed class InternalXmlDocumentationQualityFinding
{
    public InternalXmlDocumentationQualityFinding(string key, string path, int line, InternalXmlDocumentationQualityRule rule, string message)
    {
        Key = key;
        Path = path;
        Line = line;
        Rule = rule;
        Message = message;
    }

    public string Key { get; }

    public string Path { get; }

    public int Line { get; }

    public InternalXmlDocumentationQualityRule Rule { get; }

    public string Message { get; }
}

internal sealed class InternalXmlDocumentationFinding
{
    public InternalXmlDocumentationFinding(string key, string path, int line)
    {
        Key = key;
        Path = path;
        Line = line;
    }

    public string Key { get; }

    public string Path { get; }

    public int Line { get; }
}
