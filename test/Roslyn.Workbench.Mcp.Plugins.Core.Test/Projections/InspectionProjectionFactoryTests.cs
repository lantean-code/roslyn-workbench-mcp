using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using VisualBasicCompilationOptions = Microsoft.CodeAnalysis.VisualBasic.VisualBasicCompilationOptions;
using VisualBasicParseOptions = Microsoft.CodeAnalysis.VisualBasic.VisualBasicParseOptions;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Projections;

public sealed class InspectionProjectionFactoryTests
{
    [Fact]
    public void GIVEN_CSharpCompilationAndParseOptions_WHEN_CreatingCompilationOptionsInfo_THEN_ShouldProjectOptionsAndOrderedSymbols()
    {
        var compilationOptions = new CSharpCompilationOptions(OutputKind.ConsoleApplication)
            .WithAllowUnsafe(true)
            .WithNullableContextOptions(NullableContextOptions.Enable)
            .WithOptimizationLevel(OptimizationLevel.Release)
            .WithWarningLevel(5);
        var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp12)
            .WithPreprocessorSymbols("PROJECT_DETAILS_ZETA", "PROJECT_DETAILS_ALPHA");

        var result = InspectionProjectionFactory.CreateCompilationOptionsInfo(compilationOptions, parseOptions);

        result.OutputKind.Should().Be(OutputKind.ConsoleApplication.ToString());
        result.NullableContext.Should().Be(NullableContextOptions.Enable.ToString());
        result.AllowUnsafe.Should().BeTrue();
        result.OptimizationLevel.Should().Be(OptimizationLevel.Release.ToString());
        result.WarningLevel.Should().Be(5);
        result.PreprocessorSymbols.Should().Equal("PROJECT_DETAILS_ALPHA", "PROJECT_DETAILS_ZETA");
    }

    [Fact]
    public void GIVEN_CompilationAndParseOptionsAreUnavailable_WHEN_CreatingCompilationOptionsInfo_THEN_ShouldReturnDefaults()
    {
        var result = InspectionProjectionFactory.CreateCompilationOptionsInfo(null, null);

        result.Should().BeEquivalentTo(new CompilationOptionsInfo());
    }

    [Fact]
    public void GIVEN_NonCSharpCompilationOptions_WHEN_CreatingCompilationOptionsInfo_THEN_ShouldProjectLanguageNeutralOptions()
    {
        var compilationOptions = new VisualBasicCompilationOptions(OutputKind.WindowsApplication)
            .WithOptimizationLevel(OptimizationLevel.Release);

        var result = InspectionProjectionFactory.CreateCompilationOptionsInfo(compilationOptions, VisualBasicParseOptions.Default);

        result.OutputKind.Should().Be(OutputKind.WindowsApplication.ToString());
        result.NullableContext.Should().BeNull();
        result.AllowUnsafe.Should().BeFalse();
        result.OptimizationLevel.Should().Be(OptimizationLevel.Release.ToString());
        result.WarningLevel.Should().Be(compilationOptions.WarningLevel);
        result.PreprocessorSymbols.Should().NotBeEmpty();
    }

    [Fact]
    public void GIVEN_CSharpParseOptions_WHEN_CreatingParseOptionsInfo_THEN_ShouldProjectOptionsAndOrderedSymbols()
    {
        var parseOptions = new CSharpParseOptions(
            LanguageVersion.CSharp12,
            DocumentationMode.Diagnose)
            .WithPreprocessorSymbols("PROJECT_DETAILS_ZETA", "PROJECT_DETAILS_ALPHA");

        var result = InspectionProjectionFactory.CreateParseOptionsInfo(parseOptions);

        result.Should().NotBeNull();
        result!.Language.Should().Be(LanguageNames.CSharp);
        result.LanguageVersion.Should().Be(LanguageVersion.CSharp12.ToDisplayString());
        result.DocumentationMode.Should().Be(DocumentationMode.Diagnose.ToString());
        result.PreprocessorSymbols.Should().Equal("PROJECT_DETAILS_ALPHA", "PROJECT_DETAILS_ZETA");
    }

    [Fact]
    public void GIVEN_ParseOptionsAreUnavailable_WHEN_CreatingParseOptionsInfo_THEN_ShouldReturnNull()
    {
        var result = InspectionProjectionFactory.CreateParseOptionsInfo(null);

        result.Should().BeNull();
    }

    [Fact]
    public void GIVEN_NonCSharpParseOptions_WHEN_CreatingParseOptionsInfo_THEN_ShouldProjectLanguageNeutralOptions()
    {
        var parseOptions = VisualBasicParseOptions.Default.WithDocumentationMode(DocumentationMode.Diagnose);

        var result = InspectionProjectionFactory.CreateParseOptionsInfo(parseOptions);

        result.Should().NotBeNull();
        result!.Language.Should().Be(LanguageNames.VisualBasic);
        result.LanguageVersion.Should().BeEmpty();
        result.DocumentationMode.Should().Be(DocumentationMode.Diagnose.ToString());
        result.PreprocessorSymbols.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GIVEN_DocumentHasAnalyzerConfigs_WHEN_CreatingAnalyzerConfigInfo_THEN_ShouldProjectOrderedPathsAndEffectiveOptions()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("public class Sample { }");
        var project = roslyn.Document.Project;
        var updatedSolution = roslyn.Solution
            .AddAnalyzerConfigDocument(
                DocumentId.CreateNewId(project.Id, ".editorconfig"),
                ".editorconfig",
                SourceText.From("""
                    root = true

                    [*.cs]
                    z_option = Z
                    a_option = A
                    """),
                filePath: "/workspace/Project/.editorconfig")
            .AddAnalyzerConfigDocument(
                DocumentId.CreateNewId(project.Id, ".globalconfig"),
                ".globalconfig",
                SourceText.From("""
                    is_global = true
                    global_level = 100
                    global_option = Global
                    """),
                filePath: "/workspace/Project/.globalconfig");
        roslyn.Workspace.TryApplyChanges(updatedSolution).Should().BeTrue();
        var document = roslyn.Workspace.CurrentSolution.GetDocument(roslyn.Document.Id)!;

        var result = await InspectionProjectionFactory.CreateAnalyzerConfigInfoAsync(
            document,
            TestContext.Current.CancellationToken);

        result.EditorConfigPaths.Should().Equal("/workspace/Project/.editorconfig");
        result.GlobalConfigPaths.Should().Equal("/workspace/Project/.globalconfig");
        result.Options.Should().ContainKey("a_option").WhoseValue.Should().Be("A");
        result.Options.Should().ContainKey("global_option").WhoseValue.Should().Be("Global");
        result.Options.Should().ContainKey("z_option").WhoseValue.Should().Be("Z");
        result.Options.Keys.Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    [Fact]
    public async Task GIVEN_DocumentHasNoSyntaxTree_WHEN_CreatingAnalyzerConfigInfo_THEN_ShouldReturnEmptyOptions()
    {
        using var document = RoslynTestFactory.CreateUnsupportedDocument();
        var project = document.Document.Project;
        var updatedSolution = document.Solution.AddAnalyzerConfigDocument(
            DocumentId.CreateNewId(project.Id, ".editorconfig"),
            ".editorconfig",
            SourceText.From("root = true"));
        document.Workspace.TryApplyChanges(updatedSolution).Should().BeTrue();
        var currentDocument = document.Workspace.CurrentSolution.GetDocument(document.Document.Id)!;

        var result = await InspectionProjectionFactory.CreateAnalyzerConfigInfoAsync(
            currentDocument,
            TestContext.Current.CancellationToken);

        result.Options.Should().BeEmpty();
        result.EditorConfigPaths.Should().Equal(".editorconfig");
        result.GlobalConfigPaths.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_CancellationIsRequested_WHEN_CreatingAnalyzerConfigInfo_THEN_ShouldThrowCancellation()
    {
        using var document = RoslynTestFactory.CreateDocument("public class Sample { }");
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        Func<Task> action = async () => await InspectionProjectionFactory.CreateAnalyzerConfigInfoAsync(
            document.Document,
            cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void GIVEN_AnalyzerFileReference_WHEN_CreatingAnalyzerInfo_THEN_ShouldProjectDisplayAndPath()
    {
        var path = typeof(InspectionProjectionFactoryTests).Assembly.Location;
        var assemblyLoader = new Mock<IAnalyzerAssemblyLoader>();
        var reference = new AnalyzerFileReference(path, assemblyLoader.Object);

        var result = InspectionProjectionFactory.CreateAnalyzerInfo(reference);

        result.DisplayName.Should().NotBeNullOrWhiteSpace();
        result.Path.Should().Be(path);
    }

    [Fact]
    public void GIVEN_AnalyzerDisplayIsUnavailable_WHEN_CreatingAnalyzerInfo_THEN_ShouldUseReferenceTypeName()
    {
        var reference = new Mock<AnalyzerReference>();
        reference.SetupGet(item => item.Display).Returns((string)null!);

        var result = InspectionProjectionFactory.CreateAnalyzerInfo(reference.Object);

        result.DisplayName.Should().Be(reference.Object.GetType().Name);
        result.Path.Should().BeNull();
    }

    [Fact]
    public async Task GIVEN_FieldPropertyLocalParameterMethodTypeAndNamespaceSymbols_WHEN_CreatingAssociatedTypeInfo_THEN_ShouldProjectSupportedSymbolTypes()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            namespace Sample;

            public class Container
            {
                public string Field = string.Empty;
                public int Property { get; set; }

                public void Method(int parameter)
                {
                    decimal local = parameter;
                }
            }
            """);
        var root = (CompilationUnitSyntax)(await document.Document.GetSyntaxRootAsync(TestContext.Current.CancellationToken))!;
        var semanticModel = (await document.Document.GetSemanticModelAsync(TestContext.Current.CancellationToken))!;
        var field = semanticModel.GetDeclaredSymbol(root.DescendantNodes().OfType<VariableDeclaratorSyntax>().Single(item => item.Identifier.ValueText == "Field"), TestContext.Current.CancellationToken)!;
        var property = semanticModel.GetDeclaredSymbol(root.DescendantNodes().OfType<PropertyDeclarationSyntax>().Single(), TestContext.Current.CancellationToken)!;
        var local = semanticModel.GetDeclaredSymbol(root.DescendantNodes().OfType<VariableDeclaratorSyntax>().Single(item => item.Identifier.ValueText == "local"), TestContext.Current.CancellationToken)!;
        var parameter = semanticModel.GetDeclaredSymbol(root.DescendantNodes().OfType<ParameterSyntax>().Single(), TestContext.Current.CancellationToken)!;
        var method = semanticModel.GetDeclaredSymbol(root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(), TestContext.Current.CancellationToken)!;
        var type = semanticModel.GetDeclaredSymbol(root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single(), TestContext.Current.CancellationToken)!;

        InspectionProjectionFactory.CreateAssociatedTypeInfo(field)!.DisplayName.Should().Be("string");
        InspectionProjectionFactory.CreateAssociatedTypeInfo(property)!.DisplayName.Should().Be("int");
        InspectionProjectionFactory.CreateAssociatedTypeInfo(local)!.DisplayName.Should().Be("decimal");
        InspectionProjectionFactory.CreateAssociatedTypeInfo(parameter)!.DisplayName.Should().Be("int");
        InspectionProjectionFactory.CreateAssociatedTypeInfo(method)!.DisplayName.Should().Be("Sample.Container");
        InspectionProjectionFactory.CreateAssociatedTypeInfo(type)!.DisplayName.Should().Be("Sample.Container");
        InspectionProjectionFactory.CreateAssociatedTypeInfo(semanticModel.Compilation.GlobalNamespace).Should().BeNull();
    }

    [Fact]
    public async Task GIVEN_SourceSymbol_WHEN_CreatingDefinitionLocation_THEN_ShouldProjectResolvedSourceLocation()
    {
        using var document = RoslynTestFactory.CreateDocument("public class Sample { }");
        var root = (CompilationUnitSyntax)(await document.Document.GetSyntaxRootAsync(TestContext.Current.CancellationToken))!;
        var semanticModel = (await document.Document.GetSemanticModelAsync(TestContext.Current.CancellationToken))!;
        var symbol = semanticModel.GetDeclaredSymbol(root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single(), TestContext.Current.CancellationToken)!;
        var sourceLocation = symbol.Locations.Single(static item => item.IsInSource);
        var projectedLocation = SelectorTestFactory.CreateResolvedLocation("Code.cs", 0, 1);
        var resolver = new Mock<IWorkspaceResolver>();
        resolver
            .Setup(item => item.CreateResolvedLocation(sourceLocation))
            .Returns(projectedLocation);

        var result = InspectionProjectionFactory.CreateDefinitionLocation(symbol, resolver.Object);

        result.Location.Should().Be(projectedLocation);
        result.IsMetadata.Should().BeFalse();
        result.MetadataName.Should().BeNull();
        result.ContainingAssembly.Should().BeNull();
        resolver.Verify(item => item.CreateResolvedLocation(sourceLocation), Times.Once);
    }

    [Fact]
    public async Task GIVEN_MetadataSymbol_WHEN_CreatingDefinitionLocation_THEN_ShouldProjectMetadataIdentity()
    {
        using var document = RoslynTestFactory.CreateDocument("public class Sample { }");
        var compilation = (await document.Document.Project.GetCompilationAsync(TestContext.Current.CancellationToken))!;
        var symbol = compilation.GetSpecialType(SpecialType.System_String);
        var resolver = new Mock<IWorkspaceResolver>();

        var result = InspectionProjectionFactory.CreateDefinitionLocation(symbol, resolver.Object);

        result.Location.Should().BeNull();
        result.IsMetadata.Should().BeTrue();
        result.MetadataName.Should().Be("string");
        result.ContainingAssembly.Should().NotBeNullOrWhiteSpace();
        resolver.Verify(item => item.CreateResolvedLocation(It.IsAny<Location>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ErrorSymbolHasNoContainingAssembly_WHEN_CreatingDefinitionLocation_THEN_ShouldLeaveContainingAssemblyEmpty()
    {
        using var document = RoslynTestFactory.CreateDocument("public class Sample { }");
        var compilation = (await document.Document.Project.GetCompilationAsync(TestContext.Current.CancellationToken))!;
        var symbol = compilation.CreateErrorTypeSymbol(null, "Missing", 0);
        var resolver = new Mock<IWorkspaceResolver>();

        var result = InspectionProjectionFactory.CreateDefinitionLocation(symbol, resolver.Object);

        result.Location.Should().BeNull();
        result.IsMetadata.Should().BeTrue();
        result.MetadataName.Should().Be("Missing");
        result.ContainingAssembly.Should().BeNull();
        resolver.Verify(item => item.CreateResolvedLocation(It.IsAny<Location>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_SymbolsHaveSupportedModifiers_WHEN_GettingModifiers_THEN_ShouldReturnEachApplicableModifier()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            using System.Threading.Tasks;

            public abstract class Base
            {
                public abstract void AbstractMethod();
                public virtual void VirtualMethod() { }
            }

            public sealed class Derived : Base
            {
                public static int Field;

                public override async void AbstractMethod()
                {
                    await Task.Yield();
                }
            }
            """);
        var root = (CompilationUnitSyntax)(await document.Document.GetSyntaxRootAsync(TestContext.Current.CancellationToken))!;
        var semanticModel = (await document.Document.GetSemanticModelAsync(TestContext.Current.CancellationToken))!;
        var baseType = semanticModel.GetDeclaredSymbol(root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single(item => item.Identifier.ValueText == "Base"), TestContext.Current.CancellationToken)!;
        var derivedType = semanticModel.GetDeclaredSymbol(root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single(item => item.Identifier.ValueText == "Derived"), TestContext.Current.CancellationToken)!;
        var abstractMethod = semanticModel.GetDeclaredSymbol(root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(item => item.Identifier.ValueText == "AbstractMethod" && item.Modifiers.Any(SyntaxKind.AbstractKeyword)), TestContext.Current.CancellationToken)!;
        var overrideMethod = semanticModel.GetDeclaredSymbol(root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(item => item.Modifiers.Any(SyntaxKind.OverrideKeyword)), TestContext.Current.CancellationToken)!;
        var virtualMethod = semanticModel.GetDeclaredSymbol(root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(item => item.Identifier.ValueText == "VirtualMethod"), TestContext.Current.CancellationToken)!;
        var field = semanticModel.GetDeclaredSymbol(root.DescendantNodes().OfType<VariableDeclaratorSyntax>().Single(item => item.Identifier.ValueText == "Field"), TestContext.Current.CancellationToken)!;

        InspectionProjectionFactory.GetModifiers(baseType).Should().Equal("abstract");
        InspectionProjectionFactory.GetModifiers(derivedType).Should().Equal("sealed");
        InspectionProjectionFactory.GetModifiers(abstractMethod).Should().Equal("abstract");
        InspectionProjectionFactory.GetModifiers(overrideMethod).Should().Equal("async", "override");
        InspectionProjectionFactory.GetModifiers(virtualMethod).Should().Equal("virtual");
        InspectionProjectionFactory.GetModifiers(field).Should().Equal("static");
    }

    [Theory]
    [InlineData(Microsoft.CodeAnalysis.DiagnosticSeverity.Hidden, Roslyn.Workbench.Mcp.Workspace.Results.DiagnosticSeverity.Hidden)]
    [InlineData(Microsoft.CodeAnalysis.DiagnosticSeverity.Info, Roslyn.Workbench.Mcp.Workspace.Results.DiagnosticSeverity.Info)]
    [InlineData(Microsoft.CodeAnalysis.DiagnosticSeverity.Warning, Roslyn.Workbench.Mcp.Workspace.Results.DiagnosticSeverity.Warning)]
    [InlineData(Microsoft.CodeAnalysis.DiagnosticSeverity.Error, Roslyn.Workbench.Mcp.Workspace.Results.DiagnosticSeverity.Error)]
    public void GIVEN_RoslynDiagnosticSeverity_WHEN_MappingSeverity_THEN_ShouldReturnContractSeverity(
        Microsoft.CodeAnalysis.DiagnosticSeverity severity,
        Roslyn.Workbench.Mcp.Workspace.Results.DiagnosticSeverity expected)
    {
        var result = InspectionProjectionFactory.MapSeverity(severity);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task GIVEN_ParametersHavePassingAndDefaultSemantics_WHEN_CreatingParameterInfo_THEN_ShouldProjectEveryField()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            #nullable enable

            public class Sample
            {
                public void Method(ref int required, int optional = 42, string? nullable = null) { }
            }
            """);
        var root = (CompilationUnitSyntax)(await document.Document.GetSyntaxRootAsync(TestContext.Current.CancellationToken))!;
        var semanticModel = (await document.Document.GetSemanticModelAsync(TestContext.Current.CancellationToken))!;
        var parameters = root.DescendantNodes().OfType<ParameterSyntax>()
            .ToDictionary(
                item => item.Identifier.ValueText,
                item => semanticModel.GetDeclaredSymbol(item, TestContext.Current.CancellationToken)!);

        var required = InspectionProjectionFactory.CreateParameterInfo(parameters["required"]);
        var optional = InspectionProjectionFactory.CreateParameterInfo(parameters["optional"]);
        var nullable = InspectionProjectionFactory.CreateParameterInfo(parameters["nullable"]);

        required.Name.Should().Be("required");
        required.Type!.DisplayName.Should().Be("int");
        required.RefKind.Should().Be(RefKind.Ref.ToString());
        required.IsOptional.Should().BeFalse();
        required.HasExplicitDefaultValue.Should().BeFalse();
        required.DefaultValue.Should().BeNull();
        optional.IsOptional.Should().BeTrue();
        optional.HasExplicitDefaultValue.Should().BeTrue();
        optional.DefaultValue.Should().Be("42");
        nullable.Type!.NullableAnnotation.Should().Be(NullableAnnotation.Annotated.ToString());
        nullable.HasExplicitDefaultValue.Should().BeTrue();
        nullable.DefaultValue.Should().BeNull();
    }

    [Fact]
    public void GIVEN_Project_WHEN_CreatingProjectAndReferenceInfo_THEN_ShouldProjectIdentitiesPathsAndFrameworks()
    {
        using var solution = RoslynTestFactory.CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "Project",
                AssemblyName = "Project.Assembly",
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Project.cs",
                        Source = "public class ProjectType { }",
                    },
                ],
            },
        ]);
        var project = solution.Solution.Projects.Single();

        var projectInfo = InspectionProjectionFactory.CreateProjectInfo(
            project,
            "Project.csproj",
            ["net10.0", "net9.0"]);
        var referenceInfo = InspectionProjectionFactory.CreateProjectReferenceInfo(project, "Project.csproj");

        projectInfo.ProjectId.Should().Be(project.Id.Id.ToString());
        projectInfo.Name.Should().Be("Project");
        projectInfo.Path.Should().Be("Project.csproj");
        projectInfo.AssemblyName.Should().Be("Project.Assembly");
        projectInfo.Language.Should().Be(LanguageNames.CSharp);
        projectInfo.TargetFrameworks.Should().Equal("net10.0", "net9.0");
        referenceInfo.ProjectId.Should().Be(project.Id.Id.ToString());
        referenceInfo.Name.Should().Be("Project");
        referenceInfo.Path.Should().Be("Project.csproj");
    }

    [Fact]
    public async Task GIVEN_FileImageAndDisplaylessMetadataReferences_WHEN_CreatingMetadataReferenceInfo_THEN_ShouldProjectAvailableIdentity()
    {
        var path = typeof(object).Assembly.Location;
        var fileReference = MetadataReference.CreateFromFile(path);
        var image = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        var imageReference = MetadataReference.CreateFromImage(image);
        var displaylessReference = new Mock<MetadataReference>(MetadataReferenceProperties.Assembly);
        displaylessReference.SetupGet(item => item.Display).Returns((string)null!);

        var fileResult = InspectionProjectionFactory.CreateMetadataReferenceInfo(fileReference);
        var imageResult = InspectionProjectionFactory.CreateMetadataReferenceInfo(imageReference);
        var displaylessResult = InspectionProjectionFactory.CreateMetadataReferenceInfo(displaylessReference.Object);

        fileResult.Display.Should().NotBeNullOrWhiteSpace();
        fileResult.Path.Should().Be(path);
        imageResult.Display.Should().NotBeNullOrWhiteSpace();
        imageResult.Path.Should().BeNull();
        displaylessResult.Display.Should().Be(displaylessReference.Object.GetType().Name);
        displaylessResult.Path.Should().BeNull();
    }

    [Fact]
    public async Task GIVEN_TypeAndNullSymbols_WHEN_CreatingTypeInfo_THEN_ShouldProjectTypeIdentityOrNull()
    {
        using var document = RoslynTestFactory.CreateDocument("#nullable enable\npublic class Sample { public string? Value { get; set; } }");
        var root = (CompilationUnitSyntax)(await document.Document.GetSyntaxRootAsync(TestContext.Current.CancellationToken))!;
        var semanticModel = (await document.Document.GetSemanticModelAsync(TestContext.Current.CancellationToken))!;
        var property = semanticModel.GetDeclaredSymbol(root.DescendantNodes().OfType<PropertyDeclarationSyntax>().Single(), TestContext.Current.CancellationToken)!;

        var result = InspectionProjectionFactory.CreateTypeInfo(property.Type);

        result!.DisplayName.Should().Be("string?");
        result.Kind.Should().Be(TypeKind.Class.ToString());
        result.NullableAnnotation.Should().Be(NullableAnnotation.Annotated.ToString());
        result.DocumentationCommentId.Should().Be("T:System.String");
        InspectionProjectionFactory.CreateTypeInfo(null).Should().BeNull();
    }
}
