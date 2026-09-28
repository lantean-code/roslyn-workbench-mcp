using Roslyn.Workbench.Mcp.ToolReferenceGenerator;

namespace Roslyn.Workbench.Mcp.Test.ToolReference;

public sealed class SecurityReferenceOptionsTests
{
    private static readonly string[] _operationalModeNames =
    [
        "inspection-only",
        "transactional",
        "approval-required",
        "autonomous-trusted",
    ];

    private static readonly string[] _requiredToolReferenceArguments =
    [
        "--output", "reference/tools",
        "--examples", "examples.json",
    ];

    [Fact]
    public void GIVEN_CompleteSecurityArguments_WHEN_ParsingOptions_THEN_ShouldReturnAbsoluteSecurityPaths()
    {
        var options = ToolReferenceGeneratorOptions.Parse(
        [
            "--output", "reference/tools",
            "--examples", "examples.json",
            "--security-output", "reference/security",
            "--security-baseline", "security-surface.json",
            "--security-manifest", "security-invariants.json",
            "--repository-root", ".",
            "--source-revision", "0123456789abcdef0123456789abcdef01234567",
            "--update-security-baseline",
        ]);

        options.SecurityReference.Should().NotBeNull();
        Path.IsPathFullyQualified(options.SecurityReference!.OutputDirectory).Should().BeTrue();
        Path.IsPathFullyQualified(options.SecurityReference.BaselineFile).Should().BeTrue();
        Path.IsPathFullyQualified(options.SecurityReference.ManifestFile).Should().BeTrue();
        Path.IsPathFullyQualified(options.SecurityReference.RepositoryRoot).Should().BeTrue();
        options.SecurityReference.SourceRevision.Should().Be("0123456789abcdef0123456789abcdef01234567");
        options.SecurityReference.UpdateBaseline.Should().BeTrue();
    }

    [Fact]
    public void GIVEN_NonCommitSourceRevision_WHEN_ParsingOptions_THEN_ShouldRejectArguments()
    {
        var arguments = new[]
        {
            "--output", "reference/tools",
            "--examples", "examples.json",
            "--security-output", "reference/security",
            "--security-baseline", "security-surface.json",
            "--security-manifest", "security-invariants.json",
            "--repository-root", ".",
            "--source-revision", "0.0.0-dev",
        };

        var action = () => ToolReferenceGeneratorOptions.Parse(arguments);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*Git commit hash*");
    }

    [Theory]
    [InlineData("--security-output", "reference/security")]
    [InlineData("--update-security-baseline")]
    public void GIVEN_IncompleteSecurityArguments_WHEN_ParsingOptions_THEN_ShouldRejectArguments(params string[] securityArguments)
    {
        var arguments = _requiredToolReferenceArguments.Concat(securityArguments).ToArray();

        var action = () => ToolReferenceGeneratorOptions.Parse(arguments);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GIVEN_OperationalModes_WHEN_FormattingNames_THEN_ShouldUseTypedPublicValues()
    {
        OperationalModeNames.All.Select(OperationalModeNames.GetName).Should().Equal(_operationalModeNames);

        SecuritySurfaceNames.GetEnumName(CommitValidationPolicy.NoNewCompilerErrors)
            .Should()
            .Be("no-new-compiler-errors");
    }

    [Fact]
    public void GIVEN_UnknownOperationalMode_WHEN_FormattingName_THEN_ShouldRejectValue()
    {
        var action = () => OperationalModeNames.GetName((OperationalMode)999);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }
}
