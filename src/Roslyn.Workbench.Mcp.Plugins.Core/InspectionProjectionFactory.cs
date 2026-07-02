using Roslyn.Workbench.Mcp.Contracts.Inspection;

using ContractDiagnosticSeverity = Roslyn.Workbench.Mcp.Contracts.Results.DiagnosticSeverity;
using ContractProjectInfo = Roslyn.Workbench.Mcp.Contracts.Inspection.ProjectInfo;
using ContractTypeInfo = Roslyn.Workbench.Mcp.Contracts.Inspection.TypeInfo;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal static class InspectionProjectionFactory
{
    public static async ValueTask<AnalyzerConfigInfo> CreateAnalyzerConfigInfoAsync(Document document, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
        var optionsProvider = document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider;
        var options = syntaxTree is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : optionsProvider.GetOptions(syntaxTree).Keys
                .OrderBy(static key => key, StringComparer.Ordinal)
                .ToDictionary(
                    static key => key,
                    key => optionsProvider.GetOptions(syntaxTree).TryGetValue(key, out var value) ? value : string.Empty,
                    StringComparer.Ordinal);

        return new AnalyzerConfigInfo
        {
            GlobalConfigPaths = document.Project.AnalyzerConfigDocuments
                .Where(static config => config.Name.EndsWith(".globalconfig", StringComparison.OrdinalIgnoreCase))
                .Select(static config => config.FilePath ?? config.Name)
                .OrderBy(static path => path, StringComparer.Ordinal)
                .ToArray(),
            EditorConfigPaths = document.Project.AnalyzerConfigDocuments
                .Where(static config => config.Name.EndsWith(".editorconfig", StringComparison.OrdinalIgnoreCase))
                .Select(static config => config.FilePath ?? config.Name)
                .OrderBy(static path => path, StringComparer.Ordinal)
                .ToArray(),
            Options = options,
        };
    }

    public static AnalyzerInfo CreateAnalyzerInfo(AnalyzerReference reference)
    {
        return new AnalyzerInfo
        {
            DisplayName = reference.Display ?? reference.GetType().Name,
            Path = (reference as AnalyzerFileReference)?.FullPath,
        };
    }

    public static ContractTypeInfo? CreateAssociatedTypeInfo(ISymbol symbol)
    {
        return symbol switch
        {
            IFieldSymbol fieldSymbol => CreateTypeInfo(fieldSymbol.Type),
            IPropertySymbol propertySymbol => CreateTypeInfo(propertySymbol.Type),
            ILocalSymbol localSymbol => CreateTypeInfo(localSymbol.Type),
            IParameterSymbol parameterSymbol => CreateTypeInfo(parameterSymbol.Type),
            IMethodSymbol methodSymbol => CreateTypeInfo(methodSymbol.ContainingType),
            ITypeSymbol typeSymbol => CreateTypeInfo(typeSymbol),
            _ => null,
        };
    }

    public static CompilationOptionsInfo CreateCompilationOptionsInfo(CompilationOptions? options)
    {
        if (options is null)
        {
            return new CompilationOptionsInfo();
        }

        return new CompilationOptionsInfo
        {
            OutputKind = options.OutputKind.ToString(),
            NullableContext = options is CSharpCompilationOptions csharpOptions ? csharpOptions.NullableContextOptions.ToString() : null,
            AllowUnsafe = options is CSharpCompilationOptions csharpCompilationOptions && csharpCompilationOptions.AllowUnsafe,
            OptimizationLevel = options.OptimizationLevel.ToString(),
            WarningLevel = options.WarningLevel,
            PreprocessorSymbols = options is CSharpCompilationOptions ? [] : [],
        };
    }

    public static DefinitionLocation CreateDefinitionLocation(ISymbol symbol, IWorkspaceResolver resolver)
    {
        var sourceLocation = symbol.Locations.FirstOrDefault(static location => location.IsInSource);
        return sourceLocation is null
            ? new DefinitionLocation
            {
                IsMetadata = true,
                MetadataName = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                ContainingAssembly = symbol.ContainingAssembly?.ToDisplayString(),
            }
            : new DefinitionLocation
            {
                Location = resolver.CreateResolvedLocation(sourceLocation),
            };
    }

    public static IReadOnlyList<string> GetModifiers(ISymbol symbol)
    {
        var modifiers = new List<string>();

        if (symbol.IsAbstract)
        {
            modifiers.Add("abstract");
        }

        if (symbol is IMethodSymbol { IsAsync: true })
        {
            modifiers.Add("async");
        }

        if (symbol.IsOverride)
        {
            modifiers.Add("override");
        }

        if (symbol.IsSealed)
        {
            modifiers.Add("sealed");
        }

        if (symbol.IsStatic)
        {
            modifiers.Add("static");
        }

        if (symbol.IsVirtual)
        {
            modifiers.Add("virtual");
        }

        return modifiers;
    }

    public static ContractDiagnosticSeverity MapSeverity(Microsoft.CodeAnalysis.DiagnosticSeverity severity)
    {
        return severity switch
        {
            Microsoft.CodeAnalysis.DiagnosticSeverity.Hidden => ContractDiagnosticSeverity.Hidden,
            Microsoft.CodeAnalysis.DiagnosticSeverity.Info => ContractDiagnosticSeverity.Info,
            Microsoft.CodeAnalysis.DiagnosticSeverity.Warning => ContractDiagnosticSeverity.Warning,
            _ => ContractDiagnosticSeverity.Error,
        };
    }

    public static ParameterInfo CreateParameterInfo(IParameterSymbol parameter)
    {
        return new ParameterInfo
        {
            Name = parameter.Name,
            Type = CreateTypeInfo(parameter.Type),
            RefKind = parameter.RefKind.ToString(),
            IsOptional = parameter.IsOptional,
            HasExplicitDefaultValue = parameter.HasExplicitDefaultValue,
            DefaultValue = parameter.HasExplicitDefaultValue ? parameter.ExplicitDefaultValue?.ToString() : null,
        };
    }

    public static ParseOptionsInfo? CreateParseOptionsInfo(ParseOptions? options)
    {
        if (options is null)
        {
            return null;
        }

        return new ParseOptionsInfo
        {
            Language = options.Language,
            LanguageVersion = options is CSharpParseOptions csharpOptions ? csharpOptions.LanguageVersion.ToDisplayString() : string.Empty,
            DocumentationMode = options.DocumentationMode.ToString(),
            PreprocessorSymbols = options is CSharpParseOptions csharpParseOptions ? csharpParseOptions.PreprocessorSymbolNames.OrderBy(static value => value, StringComparer.Ordinal).ToArray() : [],
        };
    }

    public static ContractProjectInfo CreateProjectInfo(Project project, IWorkspaceResolver resolver)
    {
        return new ContractProjectInfo
        {
            ProjectId = project.Id.Id.ToString(),
            Name = project.Name,
            Path = resolver.NormalizeProjectPath(project.FilePath ?? project.Name),
            AssemblyName = project.AssemblyName ?? string.Empty,
            Language = project.Language,
            TargetFrameworks = ProjectFileUtilities.GetTargetFrameworks(project),
        };
    }

    public static ProjectReferenceInfo CreateProjectReferenceInfo(Project project, IWorkspaceResolver resolver)
    {
        return new ProjectReferenceInfo
        {
            ProjectId = project.Id.Id.ToString(),
            Name = project.Name,
            Path = resolver.NormalizeProjectPath(project.FilePath ?? project.Name),
        };
    }

    public static MetadataReferenceInfo CreateMetadataReferenceInfo(MetadataReference reference)
    {
        return new MetadataReferenceInfo
        {
            Display = reference.Display ?? reference.GetType().Name,
            Path = reference.Display,
        };
    }

    public static ContractTypeInfo? CreateTypeInfo(ITypeSymbol? symbol)
    {
        if (symbol is null)
        {
            return null;
        }

        return new ContractTypeInfo
        {
            DisplayName = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            Kind = symbol.TypeKind.ToString(),
            NullableAnnotation = symbol.NullableAnnotation.ToString(),
            DocumentationCommentId = symbol.GetDocumentationCommentId(),
        };
    }

    public static OutlineNode[] BuildOutlineChildren(SyntaxNode syntaxNode, SemanticModel semanticModel, IWorkspaceResolver resolver, bool includeMembers, CancellationToken cancellationToken)
    {
        return syntaxNode.ChildNodes()
            .Select(node => CreateOutlineNode(node, semanticModel, resolver, includeMembers, cancellationToken))
            .Where(static node => node is not null)
            .Select(static node => node!)
            .ToArray();
    }

    public static OutlineNode? CreateOutlineNode(SyntaxNode syntaxNode, SemanticModel semanticModel, IWorkspaceResolver resolver, bool includeMembers, CancellationToken cancellationToken)
    {
        var symbol = syntaxNode switch
        {
            BaseNamespaceDeclarationSyntax namespaceDeclarationSyntax => semanticModel.GetDeclaredSymbol(namespaceDeclarationSyntax, cancellationToken),
            BaseTypeDeclarationSyntax typeDeclarationSyntax => semanticModel.GetDeclaredSymbol(typeDeclarationSyntax, cancellationToken),
            DelegateDeclarationSyntax delegateDeclarationSyntax => semanticModel.GetDeclaredSymbol(delegateDeclarationSyntax, cancellationToken),
            EnumMemberDeclarationSyntax enumMemberDeclarationSyntax => semanticModel.GetDeclaredSymbol(enumMemberDeclarationSyntax, cancellationToken),
            MethodDeclarationSyntax methodDeclarationSyntax => semanticModel.GetDeclaredSymbol(methodDeclarationSyntax, cancellationToken),
            PropertyDeclarationSyntax propertyDeclarationSyntax => semanticModel.GetDeclaredSymbol(propertyDeclarationSyntax, cancellationToken),
            EventDeclarationSyntax eventDeclarationSyntax => semanticModel.GetDeclaredSymbol(eventDeclarationSyntax, cancellationToken),
            FieldDeclarationSyntax fieldDeclarationSyntax => semanticModel.GetDeclaredSymbol(fieldDeclarationSyntax.Declaration.Variables.First(), cancellationToken),
            ConstructorDeclarationSyntax constructorDeclarationSyntax => semanticModel.GetDeclaredSymbol(constructorDeclarationSyntax, cancellationToken),
            _ => null,
        };

        if (symbol is null)
        {
            return null;
        }

        var children = includeMembers || symbol is INamespaceSymbol
            ? BuildOutlineChildren(syntaxNode, semanticModel, resolver, includeMembers, cancellationToken)
            : [];

        return new OutlineNode
        {
            Name = symbol.Name,
            Kind = symbol.Kind.ToString(),
            Accessibility = symbol.DeclaredAccessibility.ToString(),
            Modifiers = GetModifiers(symbol),
            Location = symbol.Locations.FirstOrDefault(static location => location.IsInSource) is { } location
                ? resolver.CreateResolvedLocation(location)
                : null,
            Children = children,
        };
    }
}
