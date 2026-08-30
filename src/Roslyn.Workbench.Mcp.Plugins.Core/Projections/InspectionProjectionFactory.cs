using ContractDiagnosticSeverity = Roslyn.Workbench.Mcp.Workspace.Results.DiagnosticSeverity;
using ContractProjectInfo = Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection.ProjectInfo;
using ContractTypeInfo = Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection.TypeInfo;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Projections;

internal static class InspectionProjectionFactory
{
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

    public static ProjectReferenceInfo CreateProjectReferenceInfo(Project project, string normalizedPath)
    {
        return new ProjectReferenceInfo
        {
            ProjectId = project.Id.Id.ToString(),
            Name = project.Name,
            Path = normalizedPath,
        };
    }

    public static MetadataReferenceInfo CreateMetadataReferenceInfo(MetadataReference reference)
    {
        return new MetadataReferenceInfo
        {
            Display = reference.Display ?? reference.GetType().Name,
            Path = (reference as PortableExecutableReference)?.FilePath,
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
