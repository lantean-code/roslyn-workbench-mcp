using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ModelContextProtocol;
using ModelContextProtocol.Extensions.Skills;
using ModelContextProtocol.Protocol;

#pragma warning disable MCPEXP005 // Accept the experimental Skills extension and verify all published file digests.

namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class SkillResourcesIntegrationTests
{
    [Theory]
    [InlineData("inspection-only", false)]
    [InlineData("transactional", false)]
    [InlineData("approval-required", false)]
    [InlineData("autonomous-trusted", false)]
    [InlineData("transactional", true)]
    [InlineData("approval-required", true)]
    [InlineData("autonomous-trusted", true)]
    public async Task GIVEN_PublishedHostWithoutOpenWorkspace_WHEN_ReadingSkillResources_THEN_ShouldServeCompleteOfflineSkill(
        string mode,
        bool compilerValidationRequired)
    {
        IReadOnlyList<string> arguments = compilerValidationRequired
            ? ["--commit-validation", "no-new-compiler-errors"]
            : [];

        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            additionalArguments: arguments,
            operationalMode: mode);

        try
        {
            target.ServerCapabilities.Resources.Should().NotBeNull();
            target.ServerInstructions.Should().Contain("skill://roslyn-workbench/SKILL.md");
            var instructions = target.ServerInstructions!;
            var initialGuidance = instructions[..instructions.IndexOf("Workflow:", StringComparison.Ordinal)];
            initialGuidance.Should().Contain("fully trusted C# workspaces");
            initialGuidance.Should().Contain("workflow, everyday inspection, specialist inspection");
            initialGuidance.Should().Contain("skill://roslyn-workbench/SKILL.md");
            initialGuidance.Length.Should().BeLessThanOrEqualTo(512);
            instructions.Length.Should().BeLessThanOrEqualTo(4096);
            target.ServerCapabilities.SupportsSkills().Should().BeTrue();
            var listedSkills = await target.ListSkillsAsync(TestContext.Current.CancellationToken);
            var listedSkill = listedSkills.Should().ContainSingle().Which;
            listedSkill.Uri.Should().Be("skill://roslyn-workbench/SKILL.md");
            listedSkill.Name.Should().Be("roslyn-workbench");
            listedSkill.Description.Should().NotBeNullOrWhiteSpace();
            var skill = await target.GetSkillAsync(listedSkill.Uri, TestContext.Current.CancellationToken);
            skill.Uri.Should().Be(listedSkill.Uri);
            JsonNode.DeepEquals(skill.Frontmatter, listedSkill.Frontmatter).Should().BeTrue();
            skill.Resources.Entries.Should().BeEquivalentTo(listedSkill.Resources.Entries);
            var resources = await target.ListResourcesAsync(TestContext.Current.CancellationToken);
            resources.Select(resource => resource.Uri).Should().BeEquivalentTo(
                "skill://roslyn-workbench/SKILL.md",
                "skill://roslyn-workbench/references/workspaces.md",
                "skill://roslyn-workbench/references/selectors.md",
                "skill://roslyn-workbench/references/navigation.md",
                "skill://roslyn-workbench/references/structure.md",
                "skill://roslyn-workbench/references/diagnostics.md",
                "skill://roslyn-workbench/references/flow-analysis.md",
                "skill://roslyn-workbench/references/impact-analysis.md",
                "skill://roslyn-workbench/references/transactions.md",
                "skill://roslyn-workbench/references/code-actions.md",
                "skill://roslyn-workbench/references/rename-format.md",
                "skill://roslyn-workbench/references/recovery.md");

            skill.Resources.Entries.Should().NotBeNull();
            skill.Resources.Entries!.Select(entry => entry.Uri).Should().BeEquivalentTo(resources.Select(resource => resource.Uri));

            foreach (var resource in resources)
            {
                resource.MimeType.Should().Be("text/markdown");
                resource.Description.Should().NotBeNullOrWhiteSpace();
                var result = await target.ReadSkillResourceAsync(skill, resource.Uri, TestContext.Current.CancellationToken);
                var content = result.Contents.Should().ContainSingle().Which.Should().BeOfType<TextResourceContents>().Subject;
                content.Uri.Should().Be(resource.Uri);
                content.MimeType.Should().Be("text/markdown");
                content.Text.Should().NotBeNullOrWhiteSpace();

                var relativePath = new Uri(resource.Uri).AbsolutePath.TrimStart('/');
                var canonicalPath = Path.Combine(AppContext.BaseDirectory, "TestAssets", "Skill", relativePath);
                var canonicalContent = await File.ReadAllTextAsync(canonicalPath, TestContext.Current.CancellationToken);
                content.Text.ReplaceLineEndings("\n").Should().Be(canonicalContent.ReplaceLineEndings("\n"));

                foreach (Match link in Regex.Matches(content.Text, @"\[[^\]]*\]\(([^)]+)\)", RegexOptions.None, TimeSpan.FromSeconds(1)))
                {
                    var linkedUri = new Uri(new Uri(resource.Uri), link.Groups[1].Value).AbsoluteUri;
                    resources.Select(item => item.Uri).Should().Contain(linkedUri);
                }
            }

            var readUnknown = () => target.ReadResourceAsync(
                "skill://roslyn-workbench/references/../../secret.md",
                TestContext.Current.CancellationToken);

            var exception = await readUnknown.Should().ThrowAsync<McpProtocolException>();
            exception.Which.ErrorCode.Should().Be(McpErrorCode.InvalidParams);
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }
}
