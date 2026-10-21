namespace Roslyn.Workbench.Mcp.Test.Hosting;

public sealed class RoslynWorkbenchSkillResourcesTests
{
    [Fact]
    public void GIVEN_BundledSkill_WHEN_ReadingEntry_THEN_ShouldPublishFrontmatterAndRelativeReferences()
    {
        var content = RoslynWorkbenchSkillResources.ReadSkill().Replace("\r\n", "\n", StringComparison.Ordinal);

        content.Should().StartWith("---\nname: roslyn-workbench\ndescription:");
        content.Should().Contain("references/selectors.md");
        content.Should().Contain("references/transactions.md");
        content.Should().Contain("references/recovery.md");
    }

    [Fact]
    public async Task GIVEN_BundledResources_WHEN_ReadingRepeatedlyAndConcurrently_THEN_ShouldReuseEachCachedString()
    {
        Func<string>[] readers =
        [
            RoslynWorkbenchSkillResources.ReadSkill,
            RoslynWorkbenchSkillResources.ReadWorkspaces,
            RoslynWorkbenchSkillResources.ReadSelectors,
            RoslynWorkbenchSkillResources.ReadNavigation,
            RoslynWorkbenchSkillResources.ReadStructure,
            RoslynWorkbenchSkillResources.ReadDiagnostics,
            RoslynWorkbenchSkillResources.ReadFlowAnalysis,
            RoslynWorkbenchSkillResources.ReadImpactAnalysis,
            RoslynWorkbenchSkillResources.ReadCodeActions,
            RoslynWorkbenchSkillResources.ReadRenameFormat,
            RoslynWorkbenchSkillResources.ReadTransactions,
            RoslynWorkbenchSkillResources.ReadRecovery,
        ];

        foreach (var read in readers)
        {
            var reads = Enumerable.Range(0, 16)
                .Select(_ => Task.Run(read, TestContext.Current.CancellationToken))
                .ToArray();

            var results = await Task.WhenAll(reads);
            var subsequentRead = read();

            foreach (var result in results)
            {
                ReferenceEquals(result, subsequentRead).Should().BeTrue();
            }
        }
    }

    [Fact]
    public void GIVEN_MissingEmbeddedFile_WHEN_Reading_THEN_ShouldFailWithPackagingDiagnostic()
    {
        var action = () => RoslynWorkbenchSkillResources.ReadEmbeddedFile("Missing.md");

        action.Should().Throw<InvalidOperationException>().WithMessage("*skill file 'Missing.md' is missing.*");
    }
}
