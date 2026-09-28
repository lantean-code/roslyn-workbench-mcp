using System.Text.Json.Nodes;
using Roslyn.Workbench.Mcp.ToolReferenceGenerator;

namespace Roslyn.Workbench.Mcp.Test.ToolReference;

public sealed class SecurityManifestValidatorTests
{
    [Fact]
    public void GIVEN_CompleteExactMapping_WHEN_ValidatingManifest_THEN_ShouldAcceptManifest()
    {
        var manifest = CreateManifest(CreateEntry());

        var action = () => SecurityManifestValidator.Validate(manifest, CreateSurface());

        action.Should().NotThrow();
    }

    [Fact]
    public void GIVEN_UnmappedSurface_WHEN_ValidatingManifest_THEN_ShouldRejectManifest()
    {
        var entry = CreateEntry();
        var surface = CreateSurface().ToList();
        var unmappedItem = new SecuritySurfaceItem
        {
            Key = "surface:unmapped",
            Category = "surface",
            Facts = new JsonObject(),
        };

        surface.Add(unmappedItem);

        var action = () => SecurityManifestValidator.Validate(
            CreateManifest(entry),
            surface);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*not mapped*surface:unmapped*");
    }

    [Fact]
    public void GIVEN_UnknownExactMapping_WHEN_ValidatingManifest_THEN_ShouldRejectManifest()
    {
        var entry = CopyEntry(CreateEntry(), surfaceKeys: ["surface:unknown"]);

        var action = () => SecurityManifestValidator.Validate(
            CreateManifest(entry),
            CreateSurface());

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*unknown surface key*surface:unknown*");
    }

    [Fact]
    public void GIVEN_DuplicateInvariantId_WHEN_ValidatingManifest_THEN_ShouldRejectManifest()
    {
        var entry = CreateEntry();
        var manifest = CreateManifest(entry, entry);

        var action = () => SecurityManifestValidator.Validate(manifest, CreateSurface());

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*duplicated*");
    }

    [Fact]
    public void GIVEN_EnforcedInvariantWithoutEvidence_WHEN_ValidatingManifest_THEN_ShouldRejectManifest()
    {
        var entry = CopyEntry(CreateEntry(), evidence: []);

        var action = () => SecurityManifestValidator.Validate(
            CreateManifest(entry),
            CreateSurface());

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*must identify executable evidence*");
    }

    [Fact]
    public void GIVEN_EnforcedInvariantWithoutSurface_WHEN_ValidatingManifest_THEN_ShouldRejectManifest()
    {
        var entry = CopyEntry(CreateEntry(), surfaceKeys: []);
        var action = () => SecurityManifestValidator.Validate(
            CreateManifest(entry),
            []);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*must map at least one*");
    }

    [Fact]
    public void GIVEN_UnsupportedManifestFormat_WHEN_ValidatingManifest_THEN_ShouldRejectManifest()
    {
        var manifest = new SecurityInvariantManifest
        {
            Format = "unsupported",
            Entries = [CreateEntry()],
        };

        var action = () => SecurityManifestValidator.Validate(manifest, CreateSurface());

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unsupported*format*");
    }

    [Fact]
    public void GIVEN_UnsupportedClassification_WHEN_ValidatingManifest_THEN_ShouldRejectManifest()
    {
        var entry = CreateEntry();
        entry = new SecurityInvariantEntry
        {
            Id = entry.Id,
            Title = entry.Title,
            Classification = "unsupported",
            Actor = entry.Actor,
            Outcome = entry.Outcome,
            Boundary = entry.Boundary,
            ResidualRisk = entry.ResidualRisk,
            SurfaceKeys = entry.SurfaceKeys,
            Evidence = entry.Evidence,
        };

        var action = () => SecurityManifestValidator.Validate(
            CreateManifest(entry),
            CreateSurface());

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*unsupported classification*");
    }

    [Fact]
    public void GIVEN_DuplicateSurfaceMappingWithinEntry_WHEN_ValidatingManifest_THEN_ShouldRejectManifest()
    {
        var entry = CopyEntry(CreateEntry(), surfaceKeys: ["surface:item", "surface:item"]);

        var action = () => SecurityManifestValidator.Validate(
            CreateManifest(entry),
            CreateSurface());

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*more than once*");
    }

    [Fact]
    public void GIVEN_DuplicateEvidenceReference_WHEN_ValidatingManifest_THEN_ShouldRejectManifest()
    {
        var evidence = CreateEvidence();
        var entry = CopyEntry(CreateEntry(), evidence: [evidence, evidence]);
        var action = () => SecurityManifestValidator.Validate(
            CreateManifest(entry),
            CreateSurface());

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*more than once*");
    }

    private static SecurityInvariantManifest CreateManifest(params SecurityInvariantEntry[] entries)
    {
        return new SecurityInvariantManifest
        {
            Format = "roslyn-workbench-security-invariants/v1",
            Entries = entries,
        };
    }

    private static SecurityInvariantEntry CreateEntry()
    {
        return new SecurityInvariantEntry
        {
            Id = "invariant",
            Title = "Invariant",
            Classification = "enforced",
            Actor = "Host",
            Outcome = "The outcome is maintained.",
            Boundary = "The boundary is explicit.",
            ResidualRisk = "Residual risk remains.",
            SurfaceKeys = ["surface:item"],
            Evidence = [CreateEvidence()],
        };
    }

    private static SecurityInvariantEntry CopyEntry(
        SecurityInvariantEntry entry,
        IReadOnlyList<string>? surfaceKeys = null,
        IReadOnlyList<SecurityEvidenceReference>? evidence = null)
    {
        return new SecurityInvariantEntry
        {
            Id = entry.Id,
            Title = entry.Title,
            Classification = entry.Classification,
            Actor = entry.Actor,
            Outcome = entry.Outcome,
            Boundary = entry.Boundary,
            ResidualRisk = entry.ResidualRisk,
            SurfaceKeys = surfaceKeys ?? entry.SurfaceKeys,
            Evidence = evidence ?? entry.Evidence,
        };
    }

    private static SecurityEvidenceReference CreateEvidence()
    {
        return new SecurityEvidenceReference
        {
            Level = "unit",
            Project = "test/Roslyn.Workbench.Mcp.IntegrationTest/Roslyn.Workbench.Mcp.IntegrationTest.csproj",
            Source = "test/Roslyn.Workbench.Mcp.IntegrationTest/ToolReference/SecurityManifestValidatorTests.cs",
            Test = $"{typeof(SecurityManifestValidatorTests).FullName}.{nameof(GIVEN_CompleteExactMapping_WHEN_ValidatingManifest_THEN_ShouldAcceptManifest)}",
        };
    }

    private static IReadOnlyList<SecuritySurfaceItem> CreateSurface()
    {
        return
        [
            new SecuritySurfaceItem
            {
                Key = "surface:item",
                Category = "surface",
                Facts = new JsonObject
                {
                    ["value"] = true,
                },
            },
        ];
    }
}
