using ContractDiagnosticSeverity = Roslyn.Workbench.Mcp.Workspace.Results.DiagnosticSeverity;
using ContractProjectInfo = Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection.ProjectInfo;
using ContractTypeInfo = Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection.TypeInfo;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Projections;

/// <summary>
/// Maps Roslyn symbols, diagnostics, references, and project options to inspection contracts.
/// </summary>
internal static class InspectionProjectionFactory
{
    /// <summary>
    /// Projects the analyzer-config options that apply to a document.
    /// </summary>
    /// <param name="document">The document whose analyzer configuration is required.</param>
    /// <param name="cancellationToken">The token that cancels syntax-tree resolution.</param>
    /// <returns>The document's global and syntax-tree analyzer options.</returns>
    public static async ValueTask<AnalyzerConfigInfo> CreateAnalyzerConfigInfoAsync(Document document, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);
        var optionsProvider = document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider;
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        if (syntaxTree is not null)
        {
            var analyzerOptions = optionsProvider.GetOptions(syntaxTree);
            foreach (var key in analyzerOptions.Keys.OrderBy(static key => key, StringComparer.Ordinal))
            {
                options[key] = analyzerOptions.TryGetValue(key, out var value)
                    ? value
                    : string.Empty;
            }
        }

        var globalConfigPaths = GetAnalyzerConfigPaths(document.Project.AnalyzerConfigDocuments, ".globalconfig");
        var editorConfigPaths = GetAnalyzerConfigPaths(document.Project.AnalyzerConfigDocuments, ".editorconfig");

        return new AnalyzerConfigInfo
        {
            GlobalConfigPaths = globalConfigPaths,
            EditorConfigPaths = editorConfigPaths,
            Options = options,
        };
    }

    /// <summary>
    /// Projects display information for an analyzer reference.
    /// </summary>
    /// <param name="reference">The analyzer reference to project.</param>
    /// <returns>The projected analyzer information.</returns>
    public static AnalyzerInfo CreateAnalyzerInfo(AnalyzerReference reference)
    {
        return new AnalyzerInfo
        {
            DisplayName = reference.Display ?? reference.GetType().Name,
            Path = (reference as AnalyzerFileReference)?.FullPath,
        };
    }

    /// <summary>
    /// Projects the value or return type associated with a symbol.
    /// </summary>
    /// <param name="symbol">The field, property, event, method, or parameter to inspect.</param>
    /// <returns>The associated type, or <see langword="null"/> when the symbol has no projected type.</returns>
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

    /// <summary>
    /// Projects selected compilation settings together with language-specific nullable context.
    /// </summary>
    /// <param name="options">The compilation options to project.</param>
    /// <param name="parseOptions">The parse options used to interpret language-specific settings.</param>
    /// <returns>The projected settings, or <see langword="null"/> when compilation options are unavailable.</returns>
    public static CompilationOptionsInfo? CreateCompilationOptionsInfo(CompilationOptions? options, ParseOptions? parseOptions)
    {
        if (options is null)
        {
            return null;
        }

        var preprocessorSymbols = CreatePreprocessorSymbols(parseOptions);
        return new CompilationOptionsInfo
        {
            OutputKind = options.OutputKind.ToString(),
            NullableContext = options is CSharpCompilationOptions csharpOptions ? csharpOptions.NullableContextOptions.ToString() : null,
            AllowUnsafe = options is CSharpCompilationOptions csharpCompilationOptions && csharpCompilationOptions.AllowUnsafe,
            OptimizationLevel = options.OptimizationLevel.ToString(),
            WarningLevel = options.WarningLevel,
            PreprocessorSymbols = preprocessorSymbols,
        };
    }

    /// <summary>
    /// Projects a symbol's first source definition or its metadata identity.
    /// </summary>
    /// <param name="symbol">The symbol whose definition should be projected.</param>
    /// <param name="resolver">The resolver used to create canonical source locations.</param>
    /// <returns>The source or metadata definition location.</returns>
    public static DefinitionLocation CreateDefinitionLocation(ISymbol symbol, IWorkspaceResolver resolver)
    {
        var sourceLocation = symbol.Locations.FirstOrDefault(static location => location.IsInSource);
        if (sourceLocation is null)
        {
            return new DefinitionLocation
            {
                IsMetadata = true,
                MetadataName = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                ContainingAssembly = symbol.ContainingAssembly?.ToDisplayString(),
            };
        }

        return new DefinitionLocation
        {
            Location = resolver.CreateResolvedLocation(sourceLocation),
        };
    }

    /// <summary>
    /// Projects the semantic modifiers that apply to a symbol.
    /// </summary>
    /// <param name="symbol">The symbol to inspect.</param>
    /// <returns>The applicable modifiers in stable projection order.</returns>
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

    /// <summary>
    /// Maps a Roslyn diagnostic severity to the published contract value.
    /// </summary>
    /// <param name="severity">The Roslyn severity to map.</param>
    /// <returns>The corresponding contract severity.</returns>
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

    /// <summary>
    /// Projects a method or callable parameter.
    /// </summary>
    /// <param name="parameter">The parameter symbol to project.</param>
    /// <returns>The projected parameter information.</returns>
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

    /// <summary>
    /// Projects language-specific parse settings.
    /// </summary>
    /// <param name="options">The parse options to project.</param>
    /// <returns>The projected settings, or <see langword="null"/> when parse options are unavailable.</returns>
    public static ParseOptionsInfo? CreateParseOptionsInfo(ParseOptions? options)
    {
        if (options is null)
        {
            return null;
        }

        string? languageVersion = null;
        var preprocessorSymbols = CreatePreprocessorSymbols(options);
        if (options is CSharpParseOptions csharpOptions)
        {
            languageVersion = csharpOptions.LanguageVersion.ToDisplayString();
        }

        return new ParseOptionsInfo
        {
            Language = options.Language,
            LanguageVersion = languageVersion,
            DocumentationMode = options.DocumentationMode.ToString(),
            PreprocessorSymbols = preprocessorSymbols,
        };
    }

    private static string[] CreatePreprocessorSymbols(ParseOptions? options)
    {
        return options?.PreprocessorSymbolNames
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray()
            ?? [];
    }

    /// <summary>
    /// Projects high-level identity and framework information for a project.
    /// </summary>
    /// <param name="project">The project to project.</param>
    /// <param name="normalizedPath">The normalized project file path.</param>
    /// <param name="targetFrameworks">The project's target-framework identities.</param>
    /// <returns>The projected project information.</returns>
    public static ContractProjectInfo CreateProjectInfo(Project project, string normalizedPath, IReadOnlyList<string> targetFrameworks)
    {
        return new ContractProjectInfo
        {
            ProjectId = project.Id.Id.ToString(),
            Name = project.Name,
            Path = normalizedPath,
            AssemblyName = project.AssemblyName,
            Language = project.Language,
            TargetFrameworks = targetFrameworks,
        };
    }

    /// <summary>
    /// Projects a referenced project's identity and normalized path.
    /// </summary>
    /// <param name="project">The referenced project.</param>
    /// <param name="normalizedPath">The normalized referenced project path.</param>
    /// <returns>The projected project-reference information.</returns>
    public static ProjectReferenceInfo CreateProjectReferenceInfo(Project project, string normalizedPath)
    {
        return new ProjectReferenceInfo
        {
            ProjectId = project.Id.Id.ToString(),
            Name = project.Name,
            Path = normalizedPath,
        };
    }

    /// <summary>
    /// Projects display information for a metadata reference.
    /// </summary>
    /// <param name="reference">The metadata reference to project.</param>
    /// <returns>The projected metadata-reference information.</returns>
    public static MetadataReferenceInfo CreateMetadataReferenceInfo(MetadataReference reference)
    {
        return new MetadataReferenceInfo
        {
            Display = reference.Display ?? reference.GetType().Name,
            Path = (reference as PortableExecutableReference)?.FilePath,
        };
    }

    /// <summary>
    /// Projects a Roslyn type symbol into the inspection contract.
    /// </summary>
    /// <param name="symbol">The type symbol to project.</param>
    /// <returns>The projected type, or <see langword="null"/> when no symbol was supplied.</returns>
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

    private static string[] GetAnalyzerConfigPaths(IEnumerable<AnalyzerConfigDocument> documents, string fileExtension)
    {
        var paths = new List<string>();
        foreach (var document in documents)
        {
            if (document.Name.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase))
            {
                paths.Add(document.FilePath ?? document.Name);
            }
        }

        return paths.OrderBy(static path => path, StringComparer.Ordinal).ToArray();
    }
}
