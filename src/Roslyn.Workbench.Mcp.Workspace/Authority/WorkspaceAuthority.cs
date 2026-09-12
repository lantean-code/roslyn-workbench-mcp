using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Workspace.Authority;

/// <summary>
/// Applies Host-owned Workspace authority using physical file-system containment.
/// </summary>
internal sealed class WorkspaceAuthority : IWorkspaceAuthority
{
    private readonly IPhysicalPathContainment _pathContainment;
    private readonly string[] _allowedRoots;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceAuthority"/> class.
    /// </summary>
    /// <param name="options">The immutable authority configuration supplied by the Host.</param>
    /// <param name="pathContainment">The service used to certify physical containment.</param>
    /// <param name="pathNormalizer">The service used to canonicalise configured roots.</param>
    public WorkspaceAuthority(
        IOptions<WorkspaceAuthorityOptions> options,
        IPhysicalPathContainment pathContainment,
        IWorkspacePathNormalizer pathNormalizer)
    {
        _pathContainment = pathContainment;
        ExternalDocumentPolicy = options.Value.ExternalDocumentPolicy;
        _allowedRoots = RemoveRedundantRoots(options.Value.AllowedRoots, pathNormalizer);
    }

    /// <inheritdoc/>
    public bool IsRestricted => _allowedRoots.Length > 0;

    /// <inheritdoc/>
    public int AllowedRootCount => _allowedRoots.Length;

    /// <inheritdoc/>
    public ExternalDocumentPolicy ExternalDocumentPolicy { get; }

    /// <inheritdoc/>
    public bool TryGetAllowedRoot(string path, out string? allowedRoot)
    {
        if (!IsRestricted)
        {
            allowedRoot = null;
            return true;
        }

        foreach (var root in _allowedRoots)
        {
            if (_pathContainment.TryGetContainedPath(root, path, out _))
            {
                allowedRoot = root;
                return true;
            }
        }

        allowedRoot = null;
        return false;
    }

    /// <inheritdoc/>
    public bool IsWorkspaceRootAllowed(string workspaceRoot)
    {
        return TryGetAllowedRoot(workspaceRoot, out _);
    }

    /// <inheritdoc/>
    public bool IsWorkspaceAllowed(string loadedPath, string workspaceRoot)
    {
        return TryGetAllowedRoot(loadedPath, out _)
            && IsWorkspaceRootAllowed(workspaceRoot);
    }

    private string[] RemoveRedundantRoots(
        IReadOnlyList<string> configuredRoots,
        IWorkspacePathNormalizer pathNormalizer)
    {
        var roots = new List<string>(configuredRoots.Count);
        foreach (var configuredRoot in configuredRoots)
        {
            if (!pathNormalizer.TryGetFullPath(configuredRoot, out var candidateRoot))
            {
                throw new InvalidOperationException("Validated Workspace authority roots must be canonicalisable.");
            }

            if (roots.Any(root => _pathContainment.TryGetContainedPath(root, candidateRoot, out _)))
            {
                continue;
            }

            roots.RemoveAll(root => _pathContainment.TryGetContainedPath(candidateRoot, root, out _));
            roots.Add(candidateRoot);
        }

        return roots.ToArray();
    }
}
