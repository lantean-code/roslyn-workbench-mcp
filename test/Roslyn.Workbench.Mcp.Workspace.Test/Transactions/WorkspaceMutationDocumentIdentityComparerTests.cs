using System.Text;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class WorkspaceMutationDocumentIdentityComparerTests
{
    private readonly WorkspaceMutationDocumentIdentityComparer _target;

    public WorkspaceMutationDocumentIdentityComparerTests()
    {
        _target = WorkspaceMutationDocumentIdentityComparer.Instance;
    }

    [Fact]
    public void GIVEN_IdentityComponentsDiffer_WHEN_Comparing_THEN_ShouldApplyTotalOrdering()
    {
        var firstProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var secondProjectId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var baseline = CreateIdentity(
            firstProjectId,
            "/Workspace/A.cs",
            isCaseSensitive: true,
            WorkspaceMutationDocumentChangeKind.Added,
            "ContentA",
            "BytesA",
            Encoding.UTF8.WebName);

        var differentProject = CreateIdentity(
            secondProjectId,
            "/Workspace/A.cs",
            isCaseSensitive: true,
            WorkspaceMutationDocumentChangeKind.Added,
            "ContentA",
            "BytesA",
            Encoding.UTF8.WebName);

        var differentPathMode = CreateIdentity(
            firstProjectId,
            "/Workspace/A.cs",
            isCaseSensitive: false,
            WorkspaceMutationDocumentChangeKind.Added,
            "ContentA",
            "BytesA",
            Encoding.UTF8.WebName);

        var differentPath = CreateIdentity(
            firstProjectId,
            "/Workspace/B.cs",
            isCaseSensitive: true,
            WorkspaceMutationDocumentChangeKind.Added,
            "ContentA",
            "BytesA",
            Encoding.UTF8.WebName);

        var differentChangeKind = CreateIdentity(
            firstProjectId,
            "/Workspace/A.cs",
            isCaseSensitive: true,
            WorkspaceMutationDocumentChangeKind.Modified,
            "ContentA",
            "BytesA",
            Encoding.UTF8.WebName);

        var differentContentHash = CreateIdentity(
            firstProjectId,
            "/Workspace/A.cs",
            isCaseSensitive: true,
            WorkspaceMutationDocumentChangeKind.Added,
            "ContentB",
            "BytesA",
            Encoding.UTF8.WebName);

        var differentSerializedBytesHash = CreateIdentity(
            firstProjectId,
            "/Workspace/A.cs",
            isCaseSensitive: true,
            WorkspaceMutationDocumentChangeKind.Added,
            "ContentA",
            "BytesB",
            Encoding.UTF8.WebName);

        var differentEncoding = CreateIdentity(
            firstProjectId,
            "/Workspace/A.cs",
            isCaseSensitive: true,
            WorkspaceMutationDocumentChangeKind.Added,
            "ContentA",
            "BytesA",
            Encoding.Unicode.WebName);

        AssertOrdered(baseline, differentProject);
        AssertSymmetricNonEquality(baseline, differentPathMode);
        AssertOrdered(baseline, differentPath);
        AssertOrdered(baseline, differentChangeKind);
        AssertOrdered(baseline, differentContentHash);
        AssertOrdered(baseline, differentSerializedBytesHash);
        AssertSymmetricNonEquality(baseline, differentEncoding);
    }

    [Fact]
    public void GIVEN_EqualOrNullIdentities_WHEN_Comparing_THEN_ShouldFollowComparerContract()
    {
        var identity = CreateIdentity(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "/Workspace/A.cs",
            isCaseSensitive: true,
            WorkspaceMutationDocumentChangeKind.Modified,
            "ContentHash",
            "SerializedBytesHash",
            Encoding.UTF8.WebName);

        var equivalentIdentity = CreateIdentity(
            identity.ProjectId,
            identity.DocumentPath.Path,
            isCaseSensitive: true,
            identity.ChangeKind,
            identity.ContentHash,
            identity.SerializedBytesHash,
            identity.EncodingName);

        _target.Compare(identity, identity).Should().Be(0);
        _target.Compare(identity, equivalentIdentity).Should().Be(0);
        _target.Compare(null, identity).Should().BeNegative();
        _target.Compare(identity, null).Should().BePositive();
        _target.Compare(null, null).Should().Be(0);
    }

    private void AssertOrdered(
        WorkspaceMutationDocumentIdentity first,
        WorkspaceMutationDocumentIdentity second)
    {
        _target.Compare(first, second).Should().BeNegative();
        _target.Compare(second, first).Should().BePositive();
    }

    private void AssertSymmetricNonEquality(
        WorkspaceMutationDocumentIdentity first,
        WorkspaceMutationDocumentIdentity second)
    {
        var forward = _target.Compare(first, second);
        var reverse = _target.Compare(second, first);

        forward.Should().NotBe(0);
        reverse.Should().Be(-forward);
    }

    private static WorkspaceMutationDocumentIdentity CreateIdentity(
        Guid projectId,
        string path,
        bool isCaseSensitive,
        WorkspaceMutationDocumentChangeKind changeKind,
        string contentHash,
        string serializedBytesHash,
        string encodingName)
    {
        var documentPath = new FileSystemPathKey(path, isCaseSensitive);
        var identity = new WorkspaceMutationDocumentIdentity
        {
            ProjectId = projectId,
            DocumentPath = documentPath,
            ChangeKind = changeKind,
            ContentHash = contentHash,
            SerializedBytesHash = serializedBytesHash,
            EncodingName = encodingName,
        };

        return identity;
    }
}
