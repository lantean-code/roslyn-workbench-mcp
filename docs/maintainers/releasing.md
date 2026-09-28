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

The release workflow is manual, including development and feature releases. Ordinary pushes and pull requests never publish release packages. An explicitly selected beta may go to NuGet.org. GitHub Releases create the prerelease tags when a maintainer publishes the draft; old non-production GitHub releases/tags may be cleaned up manually. NuGet RC and production versions remain available.

Follow the manual GitFlow pattern when closing a release: merge the release branch into the production branch, create the production tag, then merge that tag back into `develop`, not the production branch itself. Keep `develop` current when practical without promising a release schedule. Dispatch the stable release workflow against the exact production tag.

## Build and inspect

1. Start `release.yml` with publication disabled. Select the intended source and package destination using the branch model above, and inspect the calculated identity and rendered notes. Local development builds intentionally do not calculate a release version.
2. Review the exact `.nupkg`, `.snupkg`, manifest, checksums, SPDX SBOM, validation result, vulnerability report and evidence index retained by that build. Check package/tool identity, MCP server metadata, MIT licence, icon, README, source commit, symbols and documentation links. Reuse earlier installed-tool evidence only where the relevant contents are unchanged.
3. Confirm the Host dependency graph restored in locked mode. The automated vulnerability gate rejects malformed or unknown scan results and known high/critical findings; inspect lower-severity findings in context. Include bundled analyser/content dependencies such as Core's private AsyncFixer reference in manual review when they do not appear in the Host's transitive package list.
4. Inspect public artefacts for credentials, source-machine paths and unintended development records. An application-owned public Sentry DSN is not an authentication secret, but must not expose private keys or be confused with a caller-supplied destination. Portable PDB document paths need to be deterministic or appropriately mapped; inspect them as well as text files.
5. Review the unit/contract, component-integration, Windows/Linux `dnx` smoke and installed-tool acceptance results. Confirm the canonical built-in `tools/list` baselines, recovery-format fixtures and public plugin API analyser baselines are current. For stable 1.x plugin packages, compare the candidate package with the first supported 1.0 baseline by using .NET package compatibility validation. These are release preparation, not new gates on every merge into `develop`. macOS remains best effort. The Code Action compatibility audit runs separately on schedule or deliberate dispatch when Roslyn changes; release validation does not repeat it.
6. Reuse manual scenario evidence when it answers the question. Keep expensive external-repository scenarios outside GitHub Actions. [Evidence utilities](../../tools/release/README.md) explain coverage and scenario aggregates, identity, advisory comparisons and retention. Missing old provenance is not proof of a regression and does not require rerunning every scenario.

On WSL, supply the repository's temporary artifacts path to SDK commands. `dotnet package list` does not accept `--artifacts-path`; use the equivalent `ArtifactsPath` environment property when querying the already-restored project. For example, from WSL:

```bash
ArtifactsPath=/tmp/artifacts/roslyn-workbench-mcp dotnet package list \
  --project src/Roslyn.Workbench.Mcp/Roslyn.Workbench.Mcp.csproj \
  --include-transitive --vulnerable --format json --no-restore
```

## Package and evidence generation

The workflow produces only the NuGet tool and symbol packages. It validates transient `dnx` acquisition and global-tool installation from the same release candidate on Windows and Linux. The package metadata contract test independently checks that the packed `.nupkg` declares both package types and embeds exact-version MCP metadata.

The Host dependency graph owns committed locks for the Host and each referenced source project. Regenerate them only as a deliberate dependency update with `dotnet restore src/Roslyn.Workbench.Mcp/Roslyn.Workbench.Mcp.csproj --use-lock-file --force-evaluate`, then review every graph change and run normal validation. Release jobs restore the Host graph with `--locked-mode` before building and again before generating evidence.

After acceptance succeeds, the evidence job scans the locked graph, generates and validates SPDX 2.2, checks that the SBOM contains the exact package hashes and writes the sanitised evidence set. Publication requests additionally create GitHub provenance and SBOM attestations over those hashes. The verified workflow artefact, rather than the earlier unevidenced candidate, is the only input to publication jobs.

## Notes and documentation

Maintain the next release's wording in [release-notes.md](../release/release-notes.md), independently of the eventual tag. The release workflow replaces `{{VERSION}}` and `{{DOCS_VERSION}}` with GitVersion's exact `SemVer`, produces `RELEASE_NOTES.md` for GitHub and renders the same notes into the immutable documentation version. No version-named source file or follow-up commit is needed. Local/development documentation shows an explicitly unpublished preview. Approve the wording and check that its release line, channel and installation destination match the selected build before publication. Update this template for each subsequent release; the GitHub Release and versioned documentation retain previous notes.

Preserve permanent getting-started instructions; include authenticated GitHub Packages installation only when preparing notes for that destination. Keep release-specific distribution choices and limitations in the release notes. Do not imply that an unactivated site, package or support route is already public.

Keep installation, configuration, troubleshooting, security and removal guidance consistent with the build. Document plugin packages independently from the Host tool, and state which artefacts and authoring contracts a release supports. The runtime accepts trusted source-built plugins. Do not add tracking, cookies or analytics to the documentation site.

Development documentation deployment follows relevant changes merged into `develop` and updates only `dev`. Release publication writes an immutable version to the generated `gh-pages` store and deploys the complete site. Verify human pages, agent guidance, machine-readable tool documentation and package links without authentication after deployment. Do not mistake a local successful site build for that public check.

Only production releases create or update `/latest/`, and the site root redirects there once a production release exists. Before the first production release, the root opens the most recently published beta directly without creating a `/latest/` alias. Subsequent prereleases retain their own versioned documentation and do not move the production alias or root redirect. Alpha and release-candidate builds do not replace the beta fallback.

## Publication authority

GitHub Packages uses the publication job's scoped `GITHUB_TOKEN`. NuGet.org uses OIDC trusted publishing through the `Lantean` NuGet organisation, with repository owner `lantean-code`, repository `roslyn-workbench-mcp`, workflow `release.yml`, environment `nuget-org` and the exact package pattern `Lantean.Roslyn.Workbench.Mcp`. `NUGET_USER` is an environment secret containing `Lantean`. Check any temporary policy activation window before first publication. Do not silently fall back to a persistent API key or switch destination after a failure.

The `github-pages`, `github-packages` and `nuget-org` environments do not add another required-review prompt. Publication jobs alone receive the required write or OIDC permissions. Selecting `publish=true` authorises package and documentation publication, but only creates and populates a draft GitHub Release. Verify first-use authentication during an explicitly approved publication if there is no harmless authentication-only check.

The workflow leaves every GitHub Release in draft, with its prerelease flag set from the calculated channel. After the workflow succeeds, a maintainer reviews the version, target commit, notes and attached evidence, then manually publishes the draft on GitHub. Only production releases should be marked as the latest release. Package and documentation publication has already occurred at this point; this final manual action publishes the GitHub announcement and creates a missing prerelease tag. Tag-based source, symbol and package links may remain unavailable until that tag exists. Do not publish an incomplete draft left behind by a failed workflow.

A failed package publication can leave documentation and a draft already created. Retrying the same version and commit reuses documentation only when its catalogue records the same source tag, product version and commit; it never overwrites that version or moves existing aliases. The workflow reuses a draft only when its exact target commit and prerelease status match, preserving any edited notes and existing assets. A published release, conflicting draft or conflicting documentation identity stops the retry. Missing or unreadable identity data and GitHub API failures also stop publication rather than being treated as absent releases. Package-feed and duplicate-asset errors remain visible: this recovery does not overwrite packages/assets or resolve a reserved NuGet namespace.

Coverage summary/report files, checksums, the SPDX SBOM, SBOM validation, sanitised dependency findings and the release evidence index are attached automatically. After a useful manual scenario investigation, inspect its aggregate and attach `scenario-summary.json` and `scenarios.md` to that same release with explicit publication approval. Keep the detailed local run directories; upload selected raw evidence only after privacy review. Download the preceding retained scenario aggregate for the next advisory comparison. Generated evidence does not belong on a source branch.

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
