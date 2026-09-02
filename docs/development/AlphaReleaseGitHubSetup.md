# First Alpha GitHub Setup

Date: 2026-09-01

This checklist records the approved GitHub community and repository settings that cannot take effect from committed files alone. It contains no credentials. Complete each external action only after the repository files have been approved and the user explicitly authorises the corresponding GitHub change.

## Observed repository state

The read-only inventory taken on 2026-09-01 found:

| Setting | Observed | Approved target |
| --- | --- | --- |
| Repository | `lantean-code/roslyn-workbench-mcp` | No change |
| Visibility | Private | Keep private until the final approved visibility action |
| Default branch | `develop` | No change |
| Description | `Roslyn MCP server with a transaction-based workspace model for safe local analysis and refactoring.` | `A local MCP server for Roslyn-powered C# code analysis and safe, transactional refactoring.` |
| Website | Not set | `https://lantean-code.github.io/roslyn-workbench-mcp/` |
| Topics | None | `model-context-protocol`, `mcp-server`, `roslyn`, `csharp`, `dotnet`, `code-analysis`, `refactoring`, `developer-tools` |
| Licence detection | MIT | No change |
| Discussions | Disabled | Enable with the approved categories after the committed guidance is present |
| Default Actions permissions | Read and write | Change to read-only; grant write access only within approved publication jobs |
| Repository rulesets | Unavailable for this private repository on the current GitHub plan | Apply the approved lightweight rules after the repository is public or the plan supports them |

## Community activation

- [ ] Run the appropriate label synchronisation script from `tools/github` without pruning to create or update the labels from `.github/labels.json`. Inspect every unmanaged label that remains, then obtain explicit approval before running the script once with its prune option to remove GitHub's default synonym labels. Do not prune any label that should be retained.
- [ ] Preview the Bug report, Documentation problem and Focused feature proposal forms from the default branch and submit safe synthetic dry runs.
- [ ] Confirm blank Issues are disabled and the Q&A and Ideas contact links reach the intended destinations. Verify the private-vulnerability contact after the repository becomes public because GitHub does not provide that route for private repositories.
- [ ] Enable Discussions.
- [ ] Retain only maintainer-authored **Announcements**, answer-enabled **Q&A**, and **Ideas** categories; remove unused default categories.
- [ ] Confirm the Q&A and Ideas forms render against their category slugs.
- [ ] Publish and review the three seed posts in `.github/DISCUSSIONS.md` from a new user's perspective.
- [ ] Confirm repository maintainers are the only moderators and Announcements authors.

## Repository identity

- [ ] Apply the approved description, Website and topics from the inventory above.
- [ ] Keep the Plugins package, supported plugin-authoring guidance and plugin repository unpublished for alpha.

## Repository safeguards and automation

- [ ] Change the repository's default GitHub Actions workflow permissions to read-only. Keep pull-request approval disabled for Actions.
- [ ] Confirm the committed workflows use only full-SHA-pinned external actions and that pull-request jobs have read-only permissions, no publication environments, no secrets and no `pull_request_target` trigger.
- [ ] Confirm weekly Dependabot version updates target `develop`, group compatible minor and patch updates for NuGet and GitHub Actions, leave major updates individual and do not enable auto-merge.
- [ ] Confirm the synchronised `validation/full` label exists and verify adding it to a safe pull request starts integration and Linux and Windows acceptance validation.
- [ ] When repository rulesets become available, apply rules to `develop`, `main`, `release/*` and `hotfix/*` that prevent deletion and force-pushes and require the applicable committed CI checks. Do not require pull requests or approving reviews, leave feature branches unrestricted and retain the repository owner's bypass.
- [ ] Enable the dependency graph, Dependabot alerts and security updates, secret scanning and push protection where the repository and current plan make them available. Do not enable CodeQL or a third-party security scanner for alpha.

## Pages activation

- [ ] Configure GitHub Pages to use GitHub Actions as its deployment source and retain `gh-pages` as the generated, immutable version store rather than the Pages build trigger.
- [ ] Confirm the standard `github-pages` environment exists without an additional required-review gate, then set the repository variable `PAGES_DEPLOYMENT_ENABLED` to `true`.
- [ ] Run the development documentation workflow and verify that it pushes only the `dev` update to `gh-pages`, deploys the complete branch snapshot through the pinned Pages actions and serves `/dev/` without authentication.

## Publication configuration

- [ ] Confirm development and release documentation publication use the `github-pages` environment without required reviewers or secrets. Each publication job receives `contents: write`, `pages: write` and `id-token: write` and holds the shared `documentation-publication` concurrency lock from the `gh-pages` update through completed Pages deployment. No separate `release-documentation` environment is required.
- [ ] Create the `github-packages` environment without required reviewers or environment secrets. Confirm only the manual release workflow's GitHub Packages job receives `packages: write` and it uses the repository `GITHUB_TOKEN`.
- [ ] Create the `nuget-org` environment without required reviewers. Configure NuGet.org trusted publishing for repository `lantean-code/roslyn-workbench-mcp`, workflow `release.yml` and environment `nuget-org`, then add the NuGet.org account name as the `NUGET_USER` environment secret.
- [ ] If NuGet.org trusted publishing cannot be configured, stop and approve the fallback before adding a narrowly scoped NuGet API key. Do not configure both authentication routes speculatively.
- [ ] Run the release workflow with publication disabled from an allowed branch and inspect the package, symbol package, checksums, manifest, test evidence and clean installed-tool result.
- [ ] Verify GitHub Packages and NuGet.org authentication during the first explicitly approved publication if the service provides no non-publishing authentication check. Do not retry against a different destination or authentication method without approval.
- [ ] Confirm the final publication jobs alone have package, release or OIDC write permissions and that the selected channel maps to the approved destination before the first publish.

## Final public activation

Complete these steps in order as one explicitly approved activation sequence:

- [ ] Make the repository public.
- [ ] Immediately enable private vulnerability reporting, then verify that the public security policy and `https://github.com/lantean-code/roslyn-workbench-mcp/security/advisories/new` reach the private reporting form from an unauthenticated session.
- [ ] Verify the README wordmark, alpha warning, installation guidance, support routes, package links, release links and licence from an unauthenticated view.
- [ ] Verify GitHub recognises `CODE_OF_CONDUCT.md`, `CONTRIBUTING.md`, `SECURITY.md`, `SUPPORT.md` and the MIT licence as community health files.
- [ ] Confirm the public Pages site and all README/community links work without authentication.

Completion of the preceding checklist sections does not authorise the repository visibility change. Public activation still requires explicit approval.
