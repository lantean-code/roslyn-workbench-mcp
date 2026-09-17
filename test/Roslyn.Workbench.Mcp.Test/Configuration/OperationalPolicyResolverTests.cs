namespace Roslyn.Workbench.Mcp.Test.Configuration;

public sealed class OperationalPolicyResolverTests
{
    [Theory]
    [InlineData((int)OperationalMode.InspectionOnly, (int)SourceMutationPolicy.Disabled, (int)CommitAuthorisationPolicy.None, false)]
    [InlineData((int)OperationalMode.Transactional, (int)SourceMutationPolicy.Enabled, (int)CommitAuthorisationPolicy.Confirmation, true)]
    [InlineData((int)OperationalMode.ApprovalRequired, (int)SourceMutationPolicy.Enabled, (int)CommitAuthorisationPolicy.ReceiptApproval, true)]
    [InlineData((int)OperationalMode.AutonomousTrusted, (int)SourceMutationPolicy.Enabled, (int)CommitAuthorisationPolicy.None, true)]
    public void GIVEN_SupportedOperationalMode_WHEN_Resolving_THEN_ShouldReturnExpectedPolicy(
        int modeValue,
        int expectedSourceMutationValue,
        int expectedCommitAuthorisationValue,
        bool expectedSourceMutationEnabled)
    {
        var mode = (OperationalMode)modeValue;
        var expectedSourceMutation = (SourceMutationPolicy)expectedSourceMutationValue;
        var expectedCommitAuthorisation = (CommitAuthorisationPolicy)expectedCommitAuthorisationValue;
        var result = OperationalPolicyResolver.Resolve(mode);

        result.Mode.Should().Be(mode);
        result.SourceMutation.Should().Be(expectedSourceMutation);
        result.SourceMutationEnabled.Should().Be(expectedSourceMutationEnabled);
        result.CommitAuthorisation.Should().Be(expectedCommitAuthorisation);
        result.CommitValidation.Should().Be(CommitValidationPolicy.None);
        result.CompilerValidationRequired.Should().BeFalse();
    }

    [Fact]
    public void GIVEN_UnsupportedOperationalMode_WHEN_Resolving_THEN_ShouldThrow()
    {
        var action = () => OperationalPolicyResolver.Resolve((OperationalMode)999);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GIVEN_OperationalEnums_WHEN_ReadingValues_THEN_ShouldRetainExplicitNumericContracts()
    {
        ((int)OperationalMode.InspectionOnly).Should().Be(0);
        ((int)OperationalMode.Transactional).Should().Be(1);
        ((int)OperationalMode.ApprovalRequired).Should().Be(2);
        ((int)OperationalMode.AutonomousTrusted).Should().Be(3);
        ((int)SourceMutationPolicy.Disabled).Should().Be(0);
        ((int)SourceMutationPolicy.Enabled).Should().Be(1);
        ((int)CommitAuthorisationPolicy.None).Should().Be(0);
        ((int)CommitAuthorisationPolicy.Confirmation).Should().Be(1);
        ((int)CommitAuthorisationPolicy.ReceiptApproval).Should().Be(2);
        ((int)CommitValidationPolicy.None).Should().Be(0);
        ((int)CommitValidationPolicy.NoNewCompilerErrors).Should().Be(1);
    }

    [Fact]
    public void GIVEN_IndependentCompilerValidation_WHEN_Resolving_THEN_ShouldRetainModeAndRequireValidation()
    {
        var result = OperationalPolicyResolver.Resolve(
            OperationalMode.Transactional,
            CommitValidationPolicy.NoNewCompilerErrors);

        result.Mode.Should().Be(OperationalMode.Transactional);
        result.CompilerValidationRequired.Should().BeTrue();
    }
}
