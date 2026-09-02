# Releasing Roslyn Workbench

This is maintainer guidance, not a formal approval system. The maintainer signs commits and deliberately starts publication. Do not publish, change repository visibility or activate community features merely because a build passed.

## Version and branch model

GitVersion runs in release automation, not ordinary local development. `SemVer` identifies the package, Git tag and public release; `FullSemVer`, commit and source distance preserve provenance. Commit-message incrementing is used only for an explicit major bump; other version progression follows branch conventions and prior tags.

| Source | Version label | Default destination |
| --- | --- | --- |
| `feature/*` | `alpha` | GitHub Packages |
| `develop` | `beta` | GitHub Packages |
| `release/*` or `hotfix/*` | `rc` | NuGet.org |
| Exact stable tag | None | NuGet.org |

The release workflow is manual, including development and feature releases. Ordinary pushes and pull requests never publish release packages. An explicitly selected beta may go to NuGet.org. GitHub Releases create the prerelease tags; old non-production GitHub releases/tags may be cleaned up manually. NuGet RC and production versions remain available.

Follow the manual GitFlow pattern when closing a release: merge the release branch into the production branch, create the production tag, then merge that tag back into `develop`, not the production branch itself. Keep `develop` current when practical without promising a release schedule. Dispatch the stable release workflow against the exact production tag.

## Build and inspect

1. Start `release.yml` with publication disabled. Select the intended source and package destination using the branch model above, and inspect the calculated identity and rendered notes. Local development builds intentionally do not calculate a release version.
2. Review the exact `.nupkg`, `.snupkg`, manifest and checksums retained by that build. Check package/tool identity, MIT licence, icon, README, source commit, symbols and documentation links. Reuse earlier installed-tool evidence only where the relevant contents are unchanged.
3. Check current dependency alerts and NuGet vulnerabilities, including transitive dependencies. Include bundled analyser/content dependencies such as Core's private AsyncFixer reference, which may not appear in the Host's transitive package list. Investigate unresolved high/critical vulnerabilities affecting the release. Consider lower-severity findings in context without requiring a disposition document for every item.
4. Inspect public artefacts for credentials, source-machine paths and unintended development records. An application-owned public Sentry DSN is not an authentication secret, but must not expose private keys or be confused with a caller-supplied destination. Portable PDB document paths need to be deterministic or appropriately mapped; inspect them as well as text files.
5. Review the unit/contract, component-integration, Code Action compatibility and Windows/Linux installed-tool acceptance results. These are release preparation, not new gates on every merge into `develop`. macOS remains best effort. The Code Action audit also runs separately on schedule or deliberate dispatch when Roslyn changes.
6. Reuse manual scenario evidence when it answers the question. Keep expensive external-repository scenarios outside GitHub Actions. [Evidence utilities](../../tools/release/README.md) explain coverage and scenario aggregates, identity, advisory comparisons and retention. Missing old provenance is not proof of a regression and does not require rerunning every scenario.

On WSL, supply the repository's temporary artifacts path to SDK commands. `dotnet package list` does not accept `--artifacts-path`; use the equivalent `ArtifactsPath` environment property when querying the already-restored project. For example, from WSL:

```bash
ArtifactsPath=/tmp/artifacts/roslyn-workbench-mcp dotnet package list \
  --project src/Roslyn.Workbench.Mcp/Roslyn.Workbench.Mcp.csproj \
  --include-transitive --vulnerable --format json --no-restore
```

## Notes and documentation

Maintain the next release's wording in [release-notes.md](../release/release-notes.md), independently of the eventual tag. The release workflow replaces `{{VERSION}}` and `{{DOCS_VERSION}}` with GitVersion's exact `SemVer`, produces `RELEASE_NOTES.md` for GitHub and renders the same notes into the immutable documentation version. No version-named source file or follow-up commit is needed. Local/development documentation shows an explicitly unpublished preview. Approve the wording and check that its release line, channel and installation destination match the selected build before publication. Update this template for each subsequent release; the GitHub Release and versioned documentation retain previous notes.

Preserve permanent getting-started instructions; include authenticated GitHub Packages installation only when preparing notes for that destination. Keep release-specific distribution choices and limitations in the release notes. Do not imply that an unactivated site, package or support route is already public.

Keep installation, configuration, troubleshooting, security and removal guidance consistent with the build. Document plugin packages independently from the Host tool, and state which artefacts and authoring contracts a release supports. The runtime accepts trusted source-built plugins. Do not add tracking, cookies or analytics to the documentation site.

Development documentation deployment follows relevant changes merged into `develop` and updates only `dev`. Release publication writes an immutable version to the generated `gh-pages` store and deploys the complete site. Verify human pages, agent guidance, machine-readable tool documentation and package links without authentication after deployment. Do not mistake a local successful site build for that public check.

Only production releases create or update `/latest/`, and the site root redirects there once a production release exists. Before the first production release, the root opens the most recently published beta directly without creating a `/latest/` alias. Subsequent prereleases retain their own versioned documentation and do not move the production alias or root redirect. Alpha and release-candidate builds do not replace the beta fallback.

## Publication authority

GitHub Packages uses the publication job's scoped `GITHUB_TOKEN`. NuGet.org uses OIDC trusted publishing with repository owner `lantean-code`, repository `roslyn-workbench-mcp`, workflow `release.yml`, environment `nuget-org` and the exact package pattern `Roslyn.Workbench.Mcp`. `NUGET_USER` is an environment secret containing the NuGet account name. Check any temporary policy activation window before first publication. Do not silently fall back to a persistent API key or switch destination after a failure.

The `github-pages`, `github-packages` and `nuget-org` environments do not add another required-review prompt. Publication jobs alone receive the required write or OIDC permissions. A deliberate publication invocation is the maintainer's approval. Verify first-use authentication during an explicitly approved publication if there is no harmless authentication-only check.

Coverage summary/report files and checksums are attached automatically. After a useful manual scenario investigation, inspect its aggregate and attach `scenario-summary.json` and `scenarios.md` to that same release with explicit publication approval. Keep the detailed local run directories; upload selected raw evidence only after privacy review. Download the preceding retained scenario aggregate for the next advisory comparison. Generated evidence does not belong on a source branch.

## Repository and community activation

Before making a prepared repository public, preserve lasting engineering guidance and remove temporary worklists, dogfood logs, dated audits and superseded designs. They are available in Git history if needed, not part of the post-release documentation. Check tracked Markdown links after removal.

These setup actions require deliberate external authorisation; repeat verification when settings change:

- Set the description to `A local MCP server for Roslyn-powered C# code analysis and safe, transactional refactoring.`, Website to `https://lantean-code.github.io/roslyn-workbench-mcp/`, and topics to `model-context-protocol`, `mcp-server`, `roslyn`, `csharp`, `dotnet`, `code-analysis`, `refactoring`, `developer-tools`.
- Synchronise `.github/labels.json` through `tools/github`; prune unmanaged labels only with explicit approval. Preview the three issue forms and confirm blank Issues are disabled and support/security links are correct.
- Enable Discussions with maintainer-only Announcements, answer-enabled Q&A and Ideas. Apply the forms and seed posts from `.github/DISCUSSIONS.md`; follow `.github/TRIAGE.md` for incoming feedback.
- Keep default Actions permissions read-only, Actions PR approval disabled and auto-merge off. Enable the dependency graph, Dependabot alerts/security updates, secret scanning and push protection where available. Do not enable CodeQL or additional scanners as an implied release requirement.
- When supported by the repository plan/visibility, protect `develop`, `main`, `release/*` and `hotfix/*` from deletion and force-pushes and require the applicable CI checks. Do not require PRs/approving reviews; keep the owner's bypass and feature branches unrestricted.
- Configure Pages to deploy through Actions, preserve `gh-pages` as the generated version store, and enable `PAGES_DEPLOYMENT_ENABLED` only when deployment is approved.
- After the explicit visibility change, enable private vulnerability reporting immediately and verify the private reporting form, issue/discussion routes, community files, README assets and documentation anonymously.

The documentation workflow checks generated content and external references before deployment, then checks repository-owned public URLs after an enabled Pages deployment for a public repository. This avoids requiring new pages to exist before they can be deployed. Private or deployment-disabled runs cannot establish public-route readiness; activate Pages and community routes and verify the post-deployment check when making that transition.

Unavailable private-repository features and untested public links remain outstanding activation checks. Do not claim them complete from committed files alone.
