using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Produces versioned SHA-256 identities for validated transaction persistence sets.
/// </summary>
internal sealed class TransactionReviewIdentityService : ITransactionReviewIdentityService
{
    /// <summary>
    /// Gets the public identifier for the canonicalisation algorithm implemented by this service.
    /// </summary>
    public const string Algorithm = "sha256-rwcs-v1";

    private const string _formatMarker = "roslyn-workbench-change-set";

    /// <inheritdoc/>
    public TransactionReviewIdentity Create(
        WorkspaceSessionSnapshot session,
        IReadOnlyList<TransactionReviewDocument> documents)
    {
        var transaction = session.Transaction
            ?? throw new InvalidOperationException("A transaction review identity requires an active transaction.");

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendString(hash, _formatMarker);
        AppendInt32(hash, 1);
        AppendInt32(hash, documents.Count);
        foreach (var document in documents.OrderBy(static document => document.Path, StringComparer.Ordinal))
        {
            AppendDocument(hash, document);
        }

        var digest = Convert.ToHexStringLower(hash.GetHashAndReset());

        return new TransactionReviewIdentity
        {
            Algorithm = Algorithm,
            ChangeSetDigest = digest,
            WorkspaceId = session.Workspace.WorkspaceId,
            WorkspaceEpoch = session.Workspace.WorkspaceEpoch,
            TransactionId = transaction.TransactionId.Value,
            SnapshotId = session.CurrentSnapshotIdentity.SnapshotId.Value,
            TransactionRevision = transaction.CurrentRevision,
        };
    }

    /// <inheritdoc/>
    public bool IsBoundTo(
        TransactionReviewIdentity identity,
        WorkspaceSessionSnapshot session,
        WorkspaceTransaction transaction)
    {
        return identity.WorkspaceId == session.Workspace.WorkspaceId
            && identity.WorkspaceEpoch == session.Workspace.WorkspaceEpoch
            && identity.TransactionId == transaction.TransactionId.Value
            && identity.SnapshotId == session.CurrentSnapshotIdentity.SnapshotId.Value
            && identity.TransactionRevision == transaction.CurrentRevision;
    }

    private static void AppendDocument(IncrementalHash hash, TransactionReviewDocument document)
    {
        AppendString(hash, document.Path);
        AppendInt32(hash, (int)document.Operation);
        AppendBoolean(hash, document.OriginalExists);
        AppendString(hash, document.OriginalHash);
        AppendString(hash, document.IntendedHash);
        AppendNullableInt32(hash, document.IntendedUnixFileMode);
        AppendString(hash, document.Classification);
        AppendBoolean(hash, document.Contained);
        AppendInt32(hash, document.Projects.Count);
        foreach (var project in document.Projects)
        {
            AppendString(hash, project.Path);
            AppendString(hash, project.TargetFramework);
        }
    }

    private static void AppendBoolean(IncrementalHash hash, bool value)
    {
        Span<byte> buffer = stackalloc byte[1];
        buffer[0] = value ? (byte)1 : (byte)0;
        hash.AppendData(buffer);
    }

    private static void AppendNullableInt32(IncrementalHash hash, int? value)
    {
        if (value is null)
        {
            AppendBoolean(hash, false);
            return;
        }

        AppendBoolean(hash, true);
        AppendInt32(hash, value.Value);
    }

    private static void AppendInt32(IncrementalHash hash, int value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        hash.AppendData(buffer);
    }

    private static void AppendString(IncrementalHash hash, string? value)
    {
        if (value is null)
        {
            AppendInt32(hash, -1);
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(value);
        AppendInt32(hash, bytes.Length);
        hash.AppendData(bytes);
    }
}
