using System.Reflection;
using Roslyn.Workbench.Mcp.ToolReferenceGenerator;

namespace Roslyn.Workbench.Mcp.Test.ToolReference;

[Collection(ToolReferenceGenerationCollectionDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class SecurityEvidenceValidatorIntegrationTests
{
    private const string _integrationProject = "test/Roslyn.Workbench.Mcp.IntegrationTest/Roslyn.Workbench.Mcp.IntegrationTest.csproj";
    private const string _validatorSource = "test/Roslyn.Workbench.Mcp.IntegrationTest/ToolReference/SecurityEvidenceValidatorIntegrationTests.cs";
    private const string _validFact = "Roslyn.Workbench.Mcp.Test.ToolReference.SecurityEvidenceValidatorIntegrationTests."
        + "GIVEN_CompiledXunitFact_WHEN_ValidatingEvidence_THEN_ShouldAcceptReference";

    public static bool EvidenceValidationEnabled
    {
        get
        {
            return true;
        }
    }

    public static TheoryData<string, string, string, string, string> InvalidEvidenceBoundaries
    {
        get
        {
            var data = new TheoryData<string, string, string, string, string>();
            data.Add("unsupported", _integrationProject, _validatorSource, _validFact, "*unsupported level*");
            data.Add("unit", "../outside.csproj", _validatorSource, _validFact, "*escapes the repository root*");
            data.Add("unit", "missing.csproj", _validatorSource, _validFact, "*project*does not exist*");
            data.Add("unit", _integrationProject, "missing.cs", _validFact, "*source*does not exist*");
            data.Add("unit", _integrationProject, _validatorSource, "Unqualified", "*fully qualified*");
            return data;
        }
    }

    [Fact]
    public async Task GIVEN_CompiledXunitFact_WHEN_ValidatingEvidence_THEN_ShouldAcceptReference()
    {
        var target = new SecurityEvidenceValidator(GetRepositoryRoot());
        var evidence = CreateEvidence(
            _integrationProject,
            _validatorSource,
            nameof(GIVEN_CompiledXunitFact_WHEN_ValidatingEvidence_THEN_ShouldAcceptReference));

        var action = async () => await target.ValidateAsync([evidence], TestContext.Current.CancellationToken);

        await action.Should().NotThrowAsync();
    }

    [Theory]
    [MemberData(nameof(InvalidEvidenceBoundaries))]
    public async Task GIVEN_InvalidEvidenceBoundary_WHEN_ValidatingEvidence_THEN_ShouldRejectReference(
        string level,
        string project,
        string source,
        string test,
        string expectedMessage)
    {
        var target = new SecurityEvidenceValidator(GetRepositoryRoot());
        var evidence = new SecurityEvidenceReference
        {
            Level = level,
            Project = project,
            Source = source,
            Test = test,
        };

        var action = async () => await target.ValidateAsync([evidence], TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage(expectedMessage);
    }

    [Fact]
    public async Task GIVEN_SourceOutsideReferencedProject_WHEN_ValidatingEvidence_THEN_ShouldRejectReference()
    {
        var target = new SecurityEvidenceValidator(GetRepositoryRoot());
        var evidence = CreateEvidence(
            "test/Roslyn.Workbench.Mcp.Test/Roslyn.Workbench.Mcp.Test.csproj",
            _validatorSource,
            nameof(GIVEN_SourceOutsideReferencedProject_WHEN_ValidatingEvidence_THEN_ShouldRejectReference));

        var action = async () => await target.ValidateAsync([evidence], TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*is not compiled by project*");
    }

    [Fact]
    public async Task GIVEN_NonXunitFactAttribute_WHEN_ValidatingEvidence_THEN_ShouldRejectReference()
    {
        var target = new SecurityEvidenceValidator(GetRepositoryRoot());
        var evidence = new SecurityEvidenceReference
        {
            Level = "integration",
            Project = _integrationProject,
            Source = "test/Roslyn.Workbench.Mcp.IntegrationTest/ToolReference/EvidenceFixture/FakeFactEvidence.cs",
            Test = "Roslyn.Workbench.Mcp.Test.ToolReference.EvidenceFixture.FakeFactEvidence.MethodWithNonXunitFactAttribute",
        };

        var action = async () => await target.ValidateAsync([evidence], TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not an executable, unconditional xUnit Fact*");
    }

    [Fact(Skip = "Evidence validator must reject conditional evidence.", SkipUnless = nameof(EvidenceValidationEnabled))]
    public async Task GIVEN_ConditionalXunitFact_WHEN_ValidatingEvidence_THEN_ShouldRejectReference()
    {
        var target = new SecurityEvidenceValidator(GetRepositoryRoot());
        var evidence = CreateEvidence(
            _integrationProject,
            _validatorSource,
            nameof(GIVEN_ConditionalXunitFact_WHEN_ValidatingEvidence_THEN_ShouldRejectReference));

        var action = async () => await target.ValidateAsync([evidence], TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not an executable, unconditional xUnit Fact*");
    }

    [Fact]
    public async Task GIVEN_UnknownTestMethod_WHEN_ValidatingEvidence_THEN_ShouldRejectReference()
    {
        var target = new SecurityEvidenceValidator(GetRepositoryRoot());
        var evidence = CreateEvidence(
            _integrationProject,
            _validatorSource,
            "UnknownMethod");

        var action = async () => await target.ValidateAsync([evidence], TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*was not found*");
    }

    [Fact]
    public void GIVEN_EvaluatedTestProjectWithRunner_WHEN_ValidatingProject_THEN_ShouldAcceptProject()
    {
        const string projectXml = """
            <Project>
              <PropertyGroup><IsTestProject>true</IsTestProject></PropertyGroup>
              <ItemGroup><PackageReference Include="xunit.v3.mtp-v2" /></ItemGroup>
            </Project>
            """;

        using var directory = TemporaryDirectory.Create("security-evidence-project-tests");
        var projectPath = CreateProject(directory.DirectoryPath, projectXml);

        var action = () => SecurityEvidenceValidator.ValidateEvaluatedTestProject(projectPath, "evidence.csproj");

        action.Should().NotThrow();
    }

    [Fact]
    public void GIVEN_TestProjectPropertyOverriddenToFalse_WHEN_ValidatingProject_THEN_ShouldRejectProject()
    {
        const string projectXml = """
            <Project>
              <PropertyGroup>
                <IsTestProject>true</IsTestProject>
                <IsTestProject>false</IsTestProject>
              </PropertyGroup>
              <ItemGroup><PackageReference Include="xunit.v3.mtp-v2" /></ItemGroup>
            </Project>
            """;

        AssertEvaluatedProjectRejected(projectXml);
    }

    [Fact]
    public void GIVEN_XunitRunnerRemovedDuringEvaluation_WHEN_ValidatingProject_THEN_ShouldRejectProject()
    {
        const string projectXml = """
            <Project>
              <PropertyGroup><IsTestProject>true</IsTestProject></PropertyGroup>
              <ItemGroup>
                <PackageReference Include="xunit.v3.mtp-v2" />
                <PackageReference Remove="xunit.v3.mtp-v2" />
              </ItemGroup>
            </Project>
            """;

        AssertEvaluatedProjectRejected(projectXml);
    }

    [Fact]
    public void GIVEN_XunitRunnerBuildAssetsExcluded_WHEN_ValidatingProject_THEN_ShouldRejectProject()
    {
        const string projectXml = """
            <Project>
              <PropertyGroup><IsTestProject>true</IsTestProject></PropertyGroup>
              <ItemGroup><PackageReference Include="xunit.v3.mtp-v2" ExcludeAssets="build;buildTransitive" /></ItemGroup>
            </Project>
            """;

        AssertEvaluatedProjectRejected(projectXml);
    }

    private static SecurityEvidenceReference CreateEvidence(
        string project,
        string source,
        string method)
    {
        return new SecurityEvidenceReference
        {
            Level = "integration",
            Project = project,
            Source = source,
            Test = $"{typeof(SecurityEvidenceValidatorIntegrationTests).FullName}.{method}",
        };
    }

    private static void AssertEvaluatedProjectRejected(string projectXml)
    {
        using var directory = TemporaryDirectory.Create("security-evidence-project-tests");
        var projectPath = CreateProject(directory.DirectoryPath, projectXml);

        var action = () => SecurityEvidenceValidator.ValidateEvaluatedTestProject(projectPath, "evidence.csproj");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*IsTestProject=true*xunit.v3.mtp-v2*");
    }

    private static string CreateProject(string directory, string projectXml)
    {
        var projectPath = Path.Combine(directory, "evidence.csproj");
        File.WriteAllText(projectPath, projectXml);
        return projectPath;
    }

    private static string GetRepositoryRoot()
    {
        return typeof(SecurityEvidenceValidatorIntegrationTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(static attribute => attribute.Key == "RepositoryRoot")
            .Value
            ?? throw new InvalidOperationException("RepositoryRoot assembly metadata was not configured.");
    }
}
