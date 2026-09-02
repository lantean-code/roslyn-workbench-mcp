# First Alpha GitHub Setup

Date: 2026-09-02

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

- [x] Run the appropriate label synchronisation script from `tools/github` without pruning to create or update the labels from `.github/labels.json`. All 37 managed labels were created on 2026-09-02, including `validation/full`.
- [x] Obtain explicit approval before pruning the remaining unmanaged labels. After confirming no issue or PR used them, the explicitly approved cleanup removed `bug`, `documentation`, `duplicate`, `enhancement`, `good first issue`, `help wanted`, `invalid`, `question` and `wontfix` on 2026-09-02. The 37 managed labels remain.
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

- [x] Change the repository's default GitHub Actions workflow permissions to read-only. Keep pull-request approval disabled for Actions. Both values were verified through the GitHub API on 2026-09-02.
- [x] Confirm the committed workflows use only full-SHA-pinned external actions and that pull-request jobs have read-only permissions, no publication environments, no secrets and no `pull_request_target` trigger. The reviewed Batch 4 commit `c02c2fb` was pushed to `develop` with explicit approval on 2026-09-02.
- [x] Confirm weekly Dependabot version updates target `develop`, group compatible minor and patch updates for NuGet and GitHub Actions, leave major updates individual and do not enable auto-merge. The reviewed configuration is on `develop`; repository auto-merge remains disabled.
- [x] Confirm the synchronised `validation/full` label exists.
- [x] Verify adding `validation/full` to a safe pull request starts integration and Linux and Windows acceptance validation. Adding the label to Dependabot PR #1 started run `33645101015` with all four integration areas and both acceptance platforms. The PR changes only the Pages-action pin and was not merged.
- [ ] When repository rulesets become available, apply rules to `develop`, `main`, `release/*` and `hotfix/*` that prevent deletion and force-pushes and require the applicable committed CI checks. Do not require pull requests or approving reviews, leave feature branches unrestricted and retain the repository owner's bypass.
- [x] Enable the dependency graph, Dependabot alerts and security updates. Alerts and updates were enabled and verified on 2026-09-02; the dependency-graph API returned an SBOM containing 50 packages.
- [ ] Enable secret scanning and push protection when available. GitHub rejected enablement on 2026-09-02 because secret scanning is unavailable for this repository. The ruleset API also continues to require a plan upgrade or public visibility. Neither repository visibility nor the plan was changed; CodeQL and third-party scanners were not enabled.

## Pages activation

- [ ] Configure GitHub Pages to use GitHub Actions as its deployment source and retain `gh-pages` as the generated, immutable version store rather than the Pages build trigger.
- [ ] Confirm the standard `github-pages` environment exists without an additional required-review gate, then set the repository variable `PAGES_DEPLOYMENT_ENABLED` to `true`.
- [ ] Run the development documentation workflow and verify that it pushes only the `dev` update to `gh-pages`, deploys the complete branch snapshot through the pinned Pages actions and serves `/dev/` without authentication.

## Publication configuration

- [x] Confirm development and release documentation publication use the `github-pages` environment without required reviewers or secrets. Each publication job receives `contents: write`, `pages: write` and `id-token: write` and holds the shared `documentation-publication` concurrency lock from the `gh-pages` update through completed Pages deployment. The environment was created on 2026-09-02; no separate `release-documentation` environment is required.
- [x] Create the `github-packages` environment without required reviewers or environment secrets. Confirm only the manual release workflow's GitHub Packages job receives `packages: write` and it uses the repository `GITHUB_TOKEN`. Created and verified on 2026-09-02.
- [x] Create the `nuget-org` environment without required reviewers and add the supplied NuGet.org account name as its `NUGET_USER` environment secret. Created and verified on 2026-09-02.
- [x] Configure NuGet.org trusted publishing for owner `lantean-code`, repository `roslyn-workbench-mcp`, workflow file `release.yml` and environment `nuget-org`. The account owner supplied confirmation on 2026-09-02 showing NuGet owner `ahjephson`, the exact package pattern `Roslyn.Workbench.Mcp` and permission to push new packages and versions, not to unlist or relist them. No API-key fallback has been configured. NuGet shows the expected seven-day activation window for the private repository; check or reactivate it before the first approved publication, following [NuGet's trusted-publishing guidance](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing). The validation-only workflow does not perform the publication needed for permanent activation.
- [ ] If NuGet.org trusted publishing cannot be configured, stop and approve the fallback before adding a narrowly scoped NuGet API key. Do not configure both authentication routes speculatively.
- [ ] Run the release workflow with publication disabled from an allowed branch and inspect the package, symbol package, checksums, manifest, test evidence and clean installed-tool result.
- [ ] Verify GitHub Packages and NuGet.org authentication during the first explicitly approved publication if the service provides no non-publishing authentication check. Do not retry against a different destination or authentication method without approval.
- [x] Confirm only the approved publication jobs have package, release or OIDC write permissions and that release-channel resolution enforces the approved destinations. Live publishing remains separately approved.

## Hosted validation evidence

- The signed Batch 4 commit `c02c2fb` is on `develop` after an explicitly approved push on 2026-09-02.
- [Continuous integration run 33644464478](https://github.com/lantean-code/roslyn-workbench-mcp/actions/runs/33644464478) passed all 2,707 unit and contract tests and uploaded the `test-results-fast` artifact.
- [Documentation run 33644464802](https://github.com/lantean-code/roslyn-workbench-mcp/actions/runs/33644464802) passed its build, validation and generated `gh-pages` development update. Live Pages deployment was skipped because activation remains disabled.
- [Release-validation run 33644565902](https://github.com/lantean-code/roslyn-workbench-mcp/actions/runs/33644565902) was dispatched with `publish=false` and `destination=default`. It resolved version `0.1.0-beta.287`, built successfully and passed unit/contract, Workspace, Core and Code Action integration tests. Host integration reported 168 passes and two failures: the tool-reference assertion hard-codes `/dev/`, and the plugin-package fixture inherits release metadata while overriding its package version. Packing, candidate acceptance and publication were skipped; correction approval was requested before changing test code.
- [Full-validation run 33645101015](https://github.com/lantean-code/roslyn-workbench-mcp/actions/runs/33645101015) was triggered by applying `validation/full` to PR #1. Unit/contract, all four integration areas and Windows acceptance passed. Linux acceptance passed 68 of 70 tests: `WorkspaceWorkflowIntegrationTests` reported a newline mismatch after rename/commit, and `WorkspaceQuerySelectorIntegrationTests` reported an unexpected tool error during the linked multi-target selector scenario. These separate acceptance failures remain open; the label-trigger mechanism itself is verified.
- After the user signed and pushed correction commit `a167078`, [continuous integration run 33654359452](https://github.com/lantean-code/roslyn-workbench-mcp/actions/runs/33654359452) passed all 2,707 unit and contract tests. Integration and acceptance jobs were skipped as intended for an ordinary `develop` push. [Documentation run 33654359465](https://github.com/lantean-code/roslyn-workbench-mcp/actions/runs/33654359465) passed its build and generated development update; public Pages activation remains separate.
- [Release-validation run 33654442487](https://github.com/lantean-code/roslyn-workbench-mcp/actions/runs/33654442487) tested `a167078` with `publish=false` and `destination=default`. Build, unit/contract and all four integration areas passed, including the previously failing Host integration step. The Code Action audit passed 122 of 123 cases: `add-constructor-parameters` failed to resolve its location because its two-field selection text hard-codes CRLF while the Linux fixture uses LF. Packing, installed-package acceptance and all publication jobs were skipped. The user approved the source-derived span correction described below.

## Approved hosted-validation corrections

- Ordinary classification-label events skip all CI jobs. Eligible jobs retain cancellation for superseded builds through separate per-job and per-matrix concurrency groups, avoiding cancellation by skipped label events.
- Documentation no longer runs on PR creation, PR updates or separate manual documentation dispatch. Relevant changes reaching `develop` build and publish `dev`; the existing release workflow validates and, when publication is approved, publishes versioned documentation. Development records and analyser-only changes are excluded, while shared analyser source compiled into the runtime remains included.
- The tool-reference integration assertion uses the compiled Host's informational version instead of assuming `dev`. The plugin-package fixture removes inherited release-identity variables only from its child processes, retaining its own test package version and leaving the parent environment unchanged.
- Both full Host integration runs pass all 170 tests: one with the normal development identity and one with `0.1.0-beta.287` release metadata matching the hosted failure scenario. The changes require a signed commit and another hosted release-validation run before the release checklist can be completed.
- Scoped formatting and both builds pass without warnings or errors. The `latest-all` build reports no diagnostics in either changed test file; its 31 warnings are in unchanged code. Workflow lint, nine event-policy cases, per-job/matrix cancellation checks and 15 documentation-path cases pass. The user confirmed the corrections for staging and re-review by the same Batch 4 reviewer.
- The same Batch 4 reviewer checked the seven-file staged correction and returned no findings. Revised workflow execution still requires the signed commit and hosted rerun; the two recorded Linux acceptance failures remain separate open issues.

## Linux acceptance corrections

- The retained Linux workspace files use LF. The rename/commit test expected CRLF in its final disk-text assertion, while the linked-document copied-selection request supplied CRLF in `contextAfter`. Windows passed because its checkout matched those hard-coded assumptions.
- Both scenarios now explicitly prepare their temporary document with LF or CRLF before opening the workspace, exercising both variants on every platform. The rename assertion still checks the complete committed text, including unchanged line endings; the copied-selection request uses context matching the document. No production code or checked-in workspace assets changed.
- The complete Linux platform wrapper passes all 72 tests with both the default fixture publish and the existing locally installed `0.1.0-alpha.999` release-validation package, including both newline variants in each run. Scoped formatting and the Release build pass without warnings or errors. The extended `latest-all` build passes with three pre-existing warnings in the unchanged Host query-plugin fixture and no diagnostics in either changed acceptance-test file.
- The user confirmed these corrections for staging and re-review by the same Batch 4 reviewer. That reviewer returned no findings or material test gaps, reusing the successful formatting, build, analyser and both complete 72-test acceptance runs. The earlier seven-file correction also has a no-findings review. The user signed and pushed the corrections as `a167078` on 2026-09-02 and confirmed the NuGet trusted-publishing policy. Hosted release validation and publication/authentication validation are still outstanding, so Batch 4 is not marked complete.

## Code Action audit correction

- The constructor-parameter audit now selects the actual span from the first field declaration through the second. `InspectionSampleFixture` exposes its existing span-selection behaviour for a specified document and keeps the default-document entry point for its existing Core integration consumer. A missing start marker is checked before searching for the end marker.
- Both complete local audit runs pass all 123 tests: one with LF and one with CRLF in the generated `CandidateRefactorings.cs` fixture. Only the audit's build-output copy was converted; checked-in workspace assets were unchanged, and the generated copy was restored to CRLF afterwards. The Core integration suite also passes all 14 tests, covering the existing default-document span-selection consumer.
- Scoped formatting, the audit and Core Release builds, and the audit `latest-all` build pass with zero warnings or errors. The user confirmed the test-only correction and explicitly waived agent re-review in favour of proving it through the GitHub workflow. A subsequent signed commit and hosted release-validation run are still required.

## Final public activation

Complete these steps in order as one explicitly approved activation sequence:

- [ ] Make the repository public.
- [ ] Immediately enable private vulnerability reporting, then verify that the public security policy and `https://github.com/lantean-code/roslyn-workbench-mcp/security/advisories/new` reach the private reporting form from an unauthenticated session.
- [ ] Verify the README wordmark, alpha warning, installation guidance, support routes, package links, release links and licence from an unauthenticated view.
- [ ] Verify GitHub recognises `CODE_OF_CONDUCT.md`, `CONTRIBUTING.md`, `SECURITY.md`, `SUPPORT.md` and the MIT licence as community health files.
- [ ] Confirm the public Pages site and all README/community links work without authentication.

Completion of the preceding checklist sections does not authorise the repository visibility change. Public activation still requires explicit approval.
