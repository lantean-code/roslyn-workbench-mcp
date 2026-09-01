# First Alpha Release Worklist

Date: 2026-08-31

## Purpose

This document is the detailed execution worklist for preparing, validating and publishing the first Roslyn Workbench alpha release. [`FutureTasks.md`](FutureTasks.md) remains the canonical engineering backlog; its first-alpha task makes the unchecked work here actionable.

The alpha is an explicitly pre-release product. It should be usable, supportable and safe enough for external evaluation without implying the compatibility guarantees expected from v1. Every deliberate limitation must be documented, and the release guidance should make intentional, traceable publication straightforward without turning a small project's release process into a formal governance system.

## Working rules

- Work through the items in dependency order. Items explicitly identified as parallel may proceed together after their shared prerequisites are complete.
- Complete and manually approve discovery for every work item before implementing any of them. Use this full-worklist design pass to identify cross-cutting requirements, conflicts and shared infrastructure, then reconcile affected approved decisions before implementation begins.
- Use the established design, manual approval, implementation, validation, confirmation and review process for every implementation item.
- Do not publish packages, binaries, tags, releases, repository visibility changes or community features without explicit approval for that external action.
- Record validation evidence against the item that required it. A successful build alone is not evidence that a packaged consumer or installed .NET tool works.
- Keep alpha-only compatibility decisions distinct from the stable v1 contract. Deferral from the alpha must identify the user impact, mitigation and intended later milestone.
- Do not add analytics, visitor tracking, cookies or third-party feedback widgets to project-owned documentation or release sites. Use explicit GitHub Issues and Discussions for feedback.

Statuses used by this worklist are **Not started**, **In discovery**, **Awaiting approval**, **Design approved**, **In progress**, **Blocked**, **Deferred from alpha** and **Complete**. **Design approved** means the item's decisions are locked for the full-worklist design pass but its implementation has not begun.

## Implementation checklist

Use this checklist as the dependency-ordered execution view. The detailed item sections remain the authoritative requirements and evidence record.

- [x] `ALPHA-001` Define the first-alpha release shape — **Complete**

**Batch 1 — Release foundation**

- [x] `ALPHA-009` Implement one authoritative alpha version — **Complete**
- [x] `ALPHA-010` Finalise package and executable metadata — **Complete**

**Batch 2 — Documentation system**

- [ ] `ALPHA-004` Generate the versioned tool reference — **Design approved**
- [ ] `ALPHA-003` Publish the GitHub Pages documentation site — **Design approved**; implement and validate generation before the explicitly approved public deployment.

**Batch 3 — Community surface**

- [ ] `ALPHA-005` Establish contribution, support and conduct policies — **Design approved**
- [ ] `ALPHA-006` Configure issue intake and triage — **Design approved**
- [ ] `ALPHA-007` Configure GitHub Discussions and community moderation — **Design approved**
- [ ] `ALPHA-002` Prepare repository identity and public-facing metadata — **Design approved**; prepare the public surface while private and complete the visibility change only as an explicitly approved final action.

**Batch 4 — Repository and release automation**

- [ ] `ALPHA-008` Harden GitHub repository controls and automation — **Design approved**
- [ ] `ALPHA-011` Implement manually triggered release builds — **Design approved**; implement and dry-run artifact production before enabling explicitly approved publication.
- [ ] `ALPHA-013` Configure protected publication — **Design approved**

**Batch 5 — Release readiness**

- [ ] `ALPHA-012` Record manual scenario and performance analysis — **Design approved**
- [ ] `ALPHA-017` Complete security, dependency and provenance checks — **Design approved**
- [ ] `ALPHA-018` Complete release documentation and notes — **Design approved**; finish development-record cleanup before repository visibility changes.
- [ ] Complete the explicitly approved repository visibility, documentation deployment and first-prerelease publication actions — **Awaiting approval**

`ALPHA-014` through `ALPHA-016` were retired during the design pass. Their identifiers remain unused and will not be reassigned.

## Delivery sequence

1. Define the alpha contract and deliberate limitations.
2. Prepare the public repository, community routes and GitHub security controls.
3. Implement versioning, packaging and release automation.
4. Complete the alpha documentation, remove temporary development tracking and make the prepared repository public.
5. Manually publish the alpha build and perform the relevant engineering checks.

Repository/community preparation and release automation may proceed in parallel after the alpha contract is approved. The sequence is guidance for organising the work; it is not a set of formal approval gates beyond the explicit approval required before an external publication action.

## Stage 1 — Alpha release shape

### `ALPHA-001` Define the first-alpha release shape

**Status:** Complete

Decide and document the practical inputs needed by the release work:

- the GitVersion branch mapping that owns version increments and pre-release labels, with its calculated `SemVer` used unchanged for the Git tag and every public artifact;
- the branch model and release trigger policy, retaining the requirement that development and feature branches can produce distributable release artifacts only through an explicit manual workflow dispatch;
- which distribution forms are included: .NET tool, symbol packages and checksums;
- supported and best-effort operating systems and architectures;
- supported MCP clients and any client-specific limitations;
- minimum .NET SDK/runtime requirements for users;
- a concise statement that alpha tool contracts, configuration and persisted state may change before v1.

Approved decisions:

- GitVersion is authoritative for version calculation. Its `SemVer` is used unchanged for the Git tag and every public versioned artifact. Retain `FullSemVer`, commit SHA and source distance as release provenance rather than public version identity.
- Use `ContinuousDelivery` with `feature/*` labelled `alpha` and incrementing minor, `develop` labelled `beta` and incrementing minor, `release/*` labelled `rc` and incrementing minor, `hotfix/*` labelled `rc` and incrementing patch, and `main` stable with no increment. Do not retain the unused `master` alias from qbtmud.
- Commit-message incrementing is reserved for an explicit major or breaking-version bump. Minor and patch progression is derived from the previous version tag and branch rules rather than commit-message directives.
- Release-producing runs from `feature/*`, `develop`, `release/*` and `hotfix/*` require an explicit manual `workflow_dispatch`. Pull requests and ordinary pushes run CI only and cannot create distributable release artifacts or GitHub releases. A push to `main` may enter the stable release path only when its commit already has the exact GitVersion-calculated tag; otherwise it fails without creating a release.
- Publish the alpha Host as a framework-dependent .NET tool installed through the `dotnet` CLI; do not produce standalone executable archives or the Plugins NuGet package for alpha. Publish alpha and beta Host packages to GitHub Packages by default and RC and production packages to NuGet.org, while permitting explicit release approval to put a selected beta—including the initial public build—on NuGet.org. Retain portable PDBs needed by diagnostic stack frames and produce symbol packages where useful. Keep packaging and Host startup boundaries suitable for later MSI and distribution-native Linux installers without making those formats alpha requirements. Do not restrict the existing plugin runtime: a source consumer may still build and load a third-party plugin, but the alpha release does not publish the authoring package, authoring guidance or a curated plugin repository.
- Support and validate Windows x64, Linux x64 and WSL2 x64 for alpha. Treat macOS x64 and ARM64 as best effort until hosted validation exists, and make no alpha support claim for Windows ARM64 or Linux ARM64 without dedicated evidence.
- Support every MCP client that can launch and communicate with a local stdio server. Named-client runs are validation examples rather than an allow-list; optional MCP capabilities such as elicitation may depend on client support or policy and must degrade through the documented protocol outcome without narrowing base client support.
- Require the .NET 10 SDK for users while .NET 10 remains supported by Microsoft. Consumers may use any supported .NET 10 SDK; the repository's exact SDK pin applies to reproducible development and release builds rather than restricting the consumer feature band. Move the product to supported .NET versions as the Microsoft support lifecycle requires.
- Alpha releases may introduce breaking changes to tool contracts, configuration and persisted state. Release notes must identify those changes; the exact user-facing wording will be approved with the first-alpha release notes.

**Completion evidence:** The approved choices are reflected consistently in release-facing documentation and the remaining worklist items.

## Stage 2 — Public repository and community readiness

### `ALPHA-002` Prepare repository identity and public-facing metadata

**Status:** Design approved

Verify the repository name, description, topics, website/documentation link, licence detection, default branch, release visibility and package links. Review the root README as the public landing page and confirm that its status, installation path, screenshots or examples, security warning, support routes and alpha limitations match the approved contract. Remove private-development assumptions and machine-specific references from public surfaces.

Approved decisions:

- Retain `lantean-code/roslyn-workbench-mcp` as the GitHub repository, **Roslyn Workbench MCP** as the product name and `develop` as the default branch.
- Use “A local MCP server for Roslyn-powered C# code analysis and safe, transactional refactoring.” as the GitHub repository description.
- Use the GitHub topics `model-context-protocol`, `mcp-server`, `roslyn`, `csharp`, `dotnet`, `code-analysis`, `refactoring` and `developer-tools`.
- Set the GitHub Website field to the canonical documentation site at `https://lantean-code.github.io/roslyn-workbench-mcp/`.
- Keep the repository private while the alpha is prepared, including the temporary-development-record cleanup required by `ALPHA-018`. Make it public only as an explicitly approved final action after the documentation site, community routes and security controls are ready and before publishing the first prerelease. Validate Pages, clean installation guidance and public links without authentication under the resulting release conditions.
- Use the root README as a concise product landing page with a prominent alpha and compatibility warning, the product and transaction-safety model, the shortest supported .NET tool installation and MCP client configuration, supported platforms and .NET requirement, a representative workflow, and clear links to documentation, security reporting, support, contributing, the Host package and releases. State that the Plugins package and a supported plugin-authoring route are not published for alpha. Prefer a compact protocol or transaction example over screenshots because the product has no visual interface.
- Use the locked wordmark from `assets/roslyn-workbench-mcp-wordmark.svg` near the top of the repository README with meaningful alternative text. Retain a text heading, preserve the artwork's proportions and do not place it on a busy background.
- Link the Host package to the registry selected for that release channel and use GitHub Releases for release notes and retained release evidence. By default, publish alpha and beta packages only to GitHub Packages and publish RC and production packages to NuGet.org. Permit an explicitly approved beta, including the initial public build, to publish to NuGet.org instead. Document the GitHub Packages feed and classic-PAT authentication required for alpha/beta installation when that route is used. Add final package links after approving the identifier in `ALPHA-010`.

**Completion evidence:** A signed-off public repository inventory with every metadata field and landing-page link checked from an unauthenticated perspective.

### `ALPHA-003` Publish the GitHub Pages documentation site

**Status:** Design approved

Approved decisions:

- Publish the latest released documentation at the site root, current `develop` documentation under `/dev/`, and immutable release documentation under `/{source-tag}/`. Embedded MCP server and analyser links use the matching immutable version path. A release may update the root site but must never replace an existing version path.
- Keep documentation sources and MkDocs configuration on `develop` and release tags, but publish generated output to a dedicated `gh-pages` branch. Pull requests may build but cannot publish. A `develop` update replaces only `/dev/`; an approved release adds its immutable version directory and updates the root site. The publication workflow must refuse to overwrite an existing version directory, and `gh-pages` must not receive manual edits.
- Store the site configuration and pinned documentation dependencies in `docs/mkdocs.yml` and `docs/docs-requirements.txt`. Move the alpha release-facing documents into `docs/content`, rename their landing page from `README.md` to `index.md`, and leave `docs/development` unchanged outside the MkDocs source tree. Retain the existing plugin-authoring documents in the repository but exclude them from the alpha site until the authoring package is published for v1. Update repository source links for moved documents and prefer rendered Pages URLs on public-facing surfaces.
- Organise navigation around Home; Getting started and Configuration; Workspaces and transactions, Tool discovery and results, Code Actions, and Error reporting and privacy under **Using Roslyn Workbench**; and the generated tool reference and Agent guide under **Reference**. Do not publish an **Extending Roslyn Workbench** section for alpha.
- Strictly build affected documentation pull requests without publishing. Automatically replace only `/dev/` after an approved documentation change reaches `develop`. Release builds retain the complete versioned site as validation evidence. Only the explicitly approved release-publication workflow may add `/{source-tag}/` and update the root site. Restrict `gh-pages` writes to a least-privilege protected job with serialised publication, and never grant Pages write authority to untrusted pull-request code.
- Identify the documentation version visibly on every page. Mark `/dev/` with a prominent unreleased-behaviour warning, show the exact source tag on released versions, and provide a selector for latest, development and retained releases. When an equivalent page does not exist in the selected version, fall back to that version's home page. Keep search scoped to the version being viewed.
- Do not add analytics, tracking scripts, cookies or third-party feedback widgets. Rely on explicit GitHub Issues and Discussions for user feedback rather than introducing another privacy or consent surface.
- Apply the approved palette and restrained developer-focused visual character from `assets/README.md` to the documentation theme. Use the locked icon and wordmark without redrawing them, and keep documentation imagery focused on code structure, semantic understanding and transactional workflows.
- Validate strict MkDocs generation, internal links and anchors, navigation, generated tool-reference links and JSON, retried external links with only justified exclusions, basic rendered HTML and accessibility, exclusion of `docs/development`, and absence of analytics, tracking and unexpected external scripts. Run these checks on pull requests and revalidate the release site's generated content before publication.
- Use a pinned `mike` dependency to manage generated versions on `gh-pages`. Use redirect aliases, permit replacement of `dev`, reject publication when an exact released source-tag version already exists, move `latest` only during approved release publication, and redirect the site root to `latest`. Automation must not delete or overwrite a released version.

Replicate qbtmud's MkDocs and Material documentation approach: keep pinned documentation dependencies, explicit navigation, search, system/light/dark themes, copyable code blocks and strict builds for pull requests targeting `develop`; deploy the site to `https://lantean-code.github.io/roslyn-workbench-mcp/` when approved documentation reaches `develop`. Pin reusable GitHub Actions to reviewed commit SHAs under the repository's release-security policy.

Move the release-facing documentation into a dedicated `docs/content` tree and exclude `docs/development` from the rendered site. Use the existing documentation README as the starting point for the site home, update repository and package links to the rendered pages, and validate navigation, anchors and external links as part of the strict build.

Publish immutable versioned documentation paths for released artifacts in addition to the current site. Replace the MCP server's raw version-tagged agent-guide URL with its versioned GitHub Pages equivalent so an installed version continues to reference matching documentation. Defer replacement of plugin-analyser help links until that analyser and its authoring documentation are published for v1.

**Completion evidence:** Pull requests strictly build the site without deploying it, an approved `develop` update deploys the current site, a representative versioned build remains addressable, embedded Host links resolve to the matching rendered pages, and neither development-only nor plugin-authoring documentation is published.

### `ALPHA-004` Generate the versioned tool reference

**Status:** Design approved

Approved decisions:

- Export the built-in reference from the actual production Host composition, including Server, bundled Core plugin and Code Action tools, while forcing full output-schema generation for documentation. Reuse `McpToolProtocolFactory` and `ToolSchemaFactory` rather than reconstructing schemas through reflection or a second generator. Add internal reference metadata where required for tool area, contract types and richer XML documentation; narrative enrichment must not alter the production schema. Include optional built-in tools with their enabling configuration and fail generation when a published built-in tool lacks required reference metadata.
- Keep `catalog.json` small, with a format version, product version, source tag and commit plus each tool's name, title, area, operation kind, short summary, availability and detail URL. Put richer documentation metadata, availability, the exact production MCP `Tool` definition with annotations and full input/output schemas, and the human-page link in one `{tool-name}.json` document per tool. Publish JSON Schemas for both formats, use an explicitly versioned format identifier and deterministic ordinal ordering, and omit generation timestamps so identical inputs produce byte-for-byte identical output.
- Generate each human page with the tool name, title, area, availability, purpose, operation kind, MCP behaviour annotations, request properties and constraints, conditional and exactly-one rules, response structure, bounded collections, outcomes, continuations, common error envelope, expandable full schemas, the per-tool JSON link and related workflow documentation. Do not expose CLR type names or synthesize examples from schemas; include only explicitly authored and validated examples.
- Treat curated examples as agent-first reference content with a secondary human audience. Store a canonical structured example with its intent, tool name, request JSON, optional representative response fragment and expected outcome; render the same source in Markdown and expose it through the machine-readable reference. Represent multi-tool transaction and Code Action workflows as ordered structured sequences. Require alpha examples for workspace opening and selection, document/location/symbol selectors, bounded limits and continuations, transactions, Code Action discovery and staging, and optional error-reporting consent. Validate requests against generated schemas and exercise multi-tool workflows through existing acceptance or scenario coverage where practical; do not require an example for every tool without demonstrated need.
- Assign every built-in tool one user-facing category in internal reference metadata: Server lifecycle, Workspaces, Transactions, Error reporting, Code discovery and navigation, Code analysis, Code mutation, or Code Actions. Group human pages by category and sort tools alphabetically within a group. Retain the broader owning area (`Server`, `CorePlugin`, or `CodeAction`) separately in machine-readable metadata for provenance without making assembly boundaries the primary discovery model.
- Keep server instructions to one shortened immutable link: `Docs: https://lantean-code.github.io/roslyn-workbench-mcp/{source-tag}/agent/`. Link the machine-readable catalogue from that Agent Guide rather than adding another instruction URL or repeating per-tool links. Do not add MCP Resources solely to expose the HTTPS documentation.
- Generate the reference as part of every ordinary documentation pipeline from the Host compiled for that source: pull requests validate the complete site without publishing, `develop` deploys the complete site to `/dev/`, and approved releases deploy it to `/{source-tag}/` and update latest. Trigger the site build for relevant code, contract, schema, example, template and authored-documentation changes. Do not commit generated Markdown or JSON to `develop`; retain authored inputs and tests there and publish generated output only through the validated site artifact and `gh-pages`.

Generate the built-in Server, bundled Core plugin and Code Action tool documentation from the compiled tool catalogue and the same production schema pipeline used by the Host. Combine XML documentation and `Description` attributes into richer agent- and user-facing guidance without expanding the deliberately compact live tool declarations. Exclude third-party plugin tools because their catalogues are not known when the Host documentation is built.

Produce a human-readable grouped tool index and one detailed page per tool containing its purpose, usage guidance and full input and output contracts. Produce a small machine-readable catalogue containing tool names, summaries and detail links plus one full JSON document per tool containing its metadata and input/output schemas, allowing an agent to retrieve only the detail it needs. Include the product version and source tag, and link the catalogue once from the agent/server guidance rather than repeating rich descriptions in every live declaration.

Generate release documentation from the compiled release build and fail validation when the generated reference is incomplete, stale or differs from the production schema pipeline. Publish it through the current, development and immutable version paths defined by `ALPHA-003`.

**Completion evidence:** Every built-in published tool appears exactly once in the human and machine-readable catalogues, representative constraints and response schemas match the release Host, per-tool JSON can be consumed independently, no unknown third-party catalogue is implied, and generated output is reproducible for the same source and version.

### `ALPHA-005` Establish contribution, support and conduct policies

**Status:** Design approved

Approved decisions:

- Add `SUPPORT.md` as the single public routing summary for setup and usage questions, exploratory ideas, reproducible defects, documentation faults, actionable feature proposals, vulnerabilities and consented diagnostic submissions. Derive its destinations and wording from the approved issue-triage, Discussions and security processes rather than defining an independent workflow. Link it consistently from contributing guidance, the README, documentation and templates, and do not promise unsustainable response or resolution times.
- Adopt Contributor Covenant 3.0 as `CODE_OF_CONDUCT.md`. Retain its behavioural standards, scope, enforcement ladder and attribution, customising only the project identity and private reporting instructions. Apply it consistently to Issues, Discussions, pull requests and other project spaces.
- Direct private Code of Conduct reports to `lanteancode@gmail.com`. Do not publish a response-time promise; keep conduct handling separate from vulnerability reporting and disclose report details only as needed to investigate and enforce the policy.
- Do not accept external pull requests before the v1 release. Welcome Issues, Discussions, security reports and other feedback during alpha, but close unsolicited pull requests with a clear link to the policy rather than beginning review. Retain MIT for alpha and acknowledge that published MIT grants cannot be withdrawn. Revisit the v1 licence, inbound contribution terms and contribution workflow before opening the repository to external contributions; do not add a CLA or DCO for alpha.
- Prefer GitHub private vulnerability reporting and use `lanteancode@gmail.com` only when it is unavailable. Tell reporters not to send source, credentials or unnecessary secrets by email. Support only the latest published alpha for security fixes; use earlier alphas only for identification or reproduction and release fixes in a new current alpha rather than backporting. Make no response or remediation-time promise, coordinate disclosure privately and publish an advisory when a confirmed issue materially affects released users.

Review `CONTRIBUTING.md` and `SECURITY.md` against the actual public workflow. Decide whether to add a separate `SUPPORT.md`. Select and add an appropriate code of conduct before actively inviting community participation. State clearly where users should ask questions, propose ideas, report reproducible defects, report documentation problems and privately disclose vulnerabilities. Avoid response-time promises that the maintainers cannot sustain.

**Completion evidence:** Every public contact route is documented once, linked consistently and has an identified maintainer-facing destination.

### `ALPHA-006` Configure issue intake and triage

**Status:** Design approved

Approved decisions:

- Provide Bug report, Documentation problem and Focused feature proposal issue forms. Cover performance regressions within Bug report initially. Request release and environment identity, relevant MCP configuration and plugin context, reproducible behaviour and redacted evidence without asking for unnecessary source. Make documentation reports identify the page/version and user goal, and make feature proposals state the problem, workflow, behaviour, impact, alternatives and compatibility implications while routing exploratory ideas to Discussions. Disable blank issues; add contact links for Q&A, Ideas and private vulnerability reporting; and apply `status/needs-triage` plus the appropriate type label automatically.
- Use `type/bug`, `type/performance`, `type/docs`, `type/feature` and `type/maintenance`; `status/needs-triage`, `status/confirmed`, `status/accepted`, `status/in-progress` and `status/blocked`; `needs/information`, `needs/reproduction`, `needs/decision` and `needs/upstream`; `resolution/duplicate`, `resolution/not-planned` and `resolution/cannot-reproduce`; areas for server, workspace, core tools, Code Actions, plugins, protocol, error reporting, packaging, documentation, and CI/release; platforms for Windows, Linux, macOS, WSL and cross-platform; and critical, high, normal and low priorities. Permit at most one type, lifecycle status and priority but multiple areas/platforms. Treat `needs/*` as temporary blockers, and reserve priority assignment for maintainers.
- Review new issues at least weekly during alpha and before each release decision without promising an individual response time. Check routing and sensitive-data exposure first, classify the issue and replace `status/needs-triage` with its reviewed status. Request missing information or reproduction explicitly and manually close after 14 days without a response while welcoming reopening with the evidence. Link duplicates to one canonical issue. Use `status/confirmed` for reproduced defects and `status/accepted` for work approved in principle without implying scheduling. Explain `resolution/not-planned` decisions concisely, and do not add an automatic stale-issue bot for alpha.
- Define priority by impact and urgency rather than effort or comment volume: critical for release-blocking data loss, unsafe writes, security impact or inability to install/start on a supported platform; high for an unusable major workflow without a reasonable workaround or prohibitive performance/resource impact; normal for confirmed limited or tolerably degraded behaviour with a workaround; and low for minor inconvenience, polish, low-impact documentation or speculative enhancement. Keep private security severity out of public issue labels.
- Do not create a GitHub milestone or Project for the first alpha. Keep this worklist as the release-planning authority and link any public release-blocking issue from its owning ALPHA item. Reconsider milestones or a Project only when multiple accepted public issues require coordinated scheduling.

Create structured GitHub issue forms for at least:

- reproducible defects;
- documentation problems; and
- focused feature proposals that are ready for engineering assessment.

The defect form should request the Roslyn Workbench version, installation form, operating system and architecture, .NET SDK/runtime, MCP client, relevant configuration, reproduction steps, expected and actual behaviour, and suitably redacted diagnostics. It must direct vulnerability reports away from public issues. Disable unrestricted blank issues unless a specific justified route remains, and direct general questions or early ideas to Discussions.

Define a maintainable label taxonomy covering type, status, area and platform without creating overlapping synonyms. Include states for needs-triage, needs-reproduction, needs-information, confirmed, duplicate and blocked; define how priority is assigned rather than allowing reporters to self-assign it. Decide whether the alpha needs a milestone or GitHub Project view.

Document the triage flow from new report to closure, including duplicate handling, requests for information, inability to reproduce, accepted work, deferral and `not planned`. Define who performs triage and a realistic review cadence without promising a resolution time.

**Completion evidence:** Each form has been previewed and submitted in a safe test repository or equivalent dry run; labels and routing produce an actionable report without exposing sensitive information.

### `ALPHA-007` Configure GitHub Discussions and community moderation

**Status:** Design approved

Approved decisions:

- Enable Discussions for alpha with maintainer-only **Announcements**, answer-enabled **Q&A** for setup, usage and troubleshooting, and **Ideas** for exploratory proposals. Do not add General, Show and tell or Plugins for alpha; add a category later only when an approved community route has a demonstrated need.
- Keep questions and troubleshooting in Q&A and early proposals in Ideas. Route Roslyn Workbench plugin defects, reproducible product defects and documentation faults to issue forms; route vulnerabilities to private vulnerability reporting and conduct reports to `lanteancode@gmail.com`. Only a maintainer promotes an actionable Discussion to an Issue, linking both directions and applying normal triage. Conversion or closure does not imply acceptance or scheduling.
- Preserve the existing plugin runtime without presenting plugin authoring as an alpha release capability. Do not publish the Plugins NuGet package, plugin-authoring documentation, a plugin directory or plugin showcase listings for alpha. A source consumer may still build and load a third-party plugin at their own initiative. Defer the supported authoring surface, package publication, curated repository and its admission, trust and maintenance model to v1 preparation.
- Make repository maintainers the moderators, initially as a single-maintainer responsibility, and apply Contributor Covenant 3.0 throughout Discussions. Review Discussions with the weekly alpha triage cadence without response promises. Allow the question author or a maintainer to mark an answer; a maintainer may replace a stale or incorrect answer with an explanation and promote recurring guidance into documentation. Link duplicates to a canonical Discussion, lock only when further replies are misleading, repetitive or violate conduct, and do not add automatic stale closure.

Decide whether Discussions will be enabled for the alpha. If enabled, configure a small purposeful category set, initially:

- **Announcements** for maintainer-authored release and project updates;
- **Q&A** for setup, usage and troubleshooting questions, with answer marking enabled;
- **Ideas** for exploratory proposals that are not yet actionable issues.

Document the boundary between Discussions, Issues and private security reports. Define moderation ownership, conduct enforcement, when a discussion should become an issue, how accepted answers are maintained and how abandoned or duplicate topics are handled. Before announcing the repository, seed Announcements with a welcome and project-status post, Q&A with guidance for asking an answerable support question, and Ideas with guidance for proposing and discussing an early idea.

**Completion evidence:** Categories, permissions, routing text and seed posts have been reviewed from a new user's perspective; announcement authors and moderators are identified.

### `ALPHA-008` Harden GitHub repository controls and automation

**Status:** Design approved

Approved decisions:

- Apply lightweight repository rules to `develop`, `main`, `release/*` and `hotfix/*`: prevent branch deletion and force-pushes and require the applicable continuous-integration checks, but do not require pull requests or approving reviews while the project has a single maintainer and does not accept external contributions. Leave feature branches unrestricted. Revisit review requirements when the contribution model changes for v1.
- Use two levels of validation guidance. Changes entering `develop`, `release/*` or `hotfix/*` should restore and build the solution, run unit and contract tests, verify the minimum test count, and apply the repository's normal compiler and analyser configuration. Run strict documentation validation as a path-relevant check when documentation or its generation changes rather than on every development build. Do not make integration, acceptance, compatibility-audit, external-scenario or `latest-all` validation part of routine entry into `develop`.
- Permit a maintainer to request full validation for a large or high-risk pull request by applying a `validation/full` label. That request adds the integration suite, Linux and Windows acceptance tests and any affected specialist checks, and those requested checks must pass before that pull request is merged. Do not infer this requirement from change size or file counts.
- Before merging a release branch into `main`, use the broader release checks that are relevant to the change and release: the full build and test suites, `latest-all` analysis, documentation and generated-reference validation, external-repository scenarios, packaging and clean-install validation, and applicable security, dependency, provenance and artifact checks. These results inform the maintainer's release decision; they are not a separately modelled candidate approval gate.
- Keep the Code Action compatibility audit separate from routine development validation. Run it periodically, when Roslyn or other compatibility inputs, the audit harness or compatibility-sensitive Code Action code changes, and while preparing a release. It does not block unrelated changes entering `develop`.
- Allow the repository owner to bypass branch rules when necessary without a separate justification or bypass log. Treat the rules as practical guardrails rather than compliance controls. Keep publication an explicit action and grant automation only the permissions required for its specific operation.
- Set GitHub Actions to read-only permissions by default and grant write access only to the specific documentation, release or publication job that requires it. Pull-request workflows must not receive publication credentials, protected environments or write access; forked pull requests must not receive secrets; and workflows must not use `pull_request_target` to build or execute contributor-controlled code. Define the protected publication environments and their approval policy under `ALPHA-013`.
- Enable weekly Dependabot updates against `develop` for NuGet and GitHub Actions. Group compatible minor and patch updates separately for those ecosystems, leave major updates as individual pull requests for explicit assessment, limit concurrent update pull requests, apply dependency and area labels, and do not auto-merge. Permit GitHub security updates outside the weekly schedule. Keep `develop` current with suitable updates as soon as practical, but do not promise dependency-update releases or backports for published alphas beyond the latest-alpha security policy approved under `ALPHA-005`.
- Pin every external GitHub Action, including GitHub-maintained actions and reusable workflows from other repositories, to a reviewed full commit SHA and retain its readable release tag in an adjacent comment. Let Dependabot propose SHA updates and review the upstream release notes and changed commit before merging them. Permit repository-local actions and workflows to use local paths without SHA pinning. This resolves `PRR-F023`.
- Do not add `CODEOWNERS` for alpha. A single maintainer and closed external contribution model provide no meaningful ownership routing, and the file could imply a review structure that does not exist. Reconsider it when v1 opens contributions or another maintainer owns a defined area.
- Copy qbtmud's compact pull-request template exactly, using `Summary`, `What Changed`, `Testing` and optional `Notes` sections. Use `Notes` for compatibility impact, release implications, risks or follow-up work when relevant. Keep the policy that external pull requests are not accepted before v1 in `CONTRIBUTING.md` rather than adding it to the template.
- Retain ordinary successful test results for 14 days, failed test and acceptance diagnostics for 30 days, periodic Code Action audit evidence for 30 days, and documentation preview artifacts for 7 days. Attach useful release artifacts and evidence to the GitHub Release where appropriate, and otherwise use GitHub's repository default for workflow logs without adding separate archival infrastructure.
- Enable GitHub's dependency graph, Dependabot alerts and security updates, secret scanning and push protection where available, and the private vulnerability-reporting route approved under `ALPHA-005`. Do not add CodeQL or third-party security-scanning services for alpha. Use Codex code review as the normal human-controlled review step before merging changes into `develop`, without attempting to automate it as a required GitHub status check.

Configure or verify:

- branch protection or repository rulesets for the default and release branches;
- required checks, review requirements and administrator/bypass policy;
- least-privilege workflow permissions and protection against untrusted pull-request contexts;
- private vulnerability reporting and the repository security policy;
- Dependabot or an equivalent reviewed update path for NuGet packages and GitHub Actions;
- full reviewed commit-SHA pinning for reusable GitHub Actions, resolving `PRR-F023`;
- CODEOWNERS only where ownership is real and sustainable;
- pull-request templates that request intent, changes, validation and compatibility risk; and
- retention settings for test logs and release evidence.

**Completion evidence:** Repository settings are recorded without secrets, workflows pass under the protected configuration, and action references and permissions have received a security review.

## Stage 3 — Versioning, artifacts and release automation

### `ALPHA-009` Implement one authoritative alpha version

**Status:** Complete

Approved decisions:

- Pin the reviewed current `GitVersion.Tool` package in a repository-local .NET tool manifest, but restore and execute it only in release workflows. Do not add `GitVersion.MsBuild` or invoke GitVersion from normal restore, build, test or development-validation paths. Give ordinary builds the fixed development identity `0.0.0-dev` without inspecting Git history. A release build fetches complete history, runs GitVersion once, passes `SemVer` explicitly to every build, packaging, documentation and manifest step as the public version, and records `FullSemVer`, commit SHA and source distance as separate provenance.
- Use `SemVer` without a `v` prefix for the Git tag, .NET tool and symbol package versions, Host `--version` output, `AssemblyInformationalVersion`, `RoslynWorkbenchSourceTag`, documentation version path and release title. Use deterministic `Major.Minor.Patch.0` values for both `AssemblyVersion` and `FileVersion`. Store commit SHA, `FullSemVer` and source distance in separate assembly metadata and release-manifest fields rather than appending workflow- or source-specific data to the public version.
- For a manually approved prerelease from `feature/*`, `develop`, `release/*` or `hotfix/*`, create the matching `SemVer` tag at the exact selected commit when creating the GitHub prerelease. Normally name a release branch `release/{target-version}`, for example `release/0.1.0`, while requiring only the `release/*` shape in automation. For a stable release, follow the manual GitFlow path: merge `release/*` into `main`, create the stable `SemVer` tag on that merge commit, then merge the tag itself into `develop` rather than merging the `main` branch. Superseded alpha/beta GitHub prereleases, tags and GitHub Package versions may be cleaned up manually after the corresponding production version is live. RC and production packages on NuGet.org remain published, as does any beta explicitly published there; never reuse one of their versions.
- Use channel-based package publication as the default rather than an absolute restriction: alpha and beta packages go to GitHub Packages, while RC and production packages go to NuGet.org. Allow explicit release approval to publish a selected beta, including the initial public release, to NuGet.org. A beta published to NuGet.org becomes a permanent public package and its version must never be reused.
- Set GitVersion's initial `next-version` to `0.1.0`, producing `0.1.0-alpha.N`, `0.1.0-beta.N`, `0.1.0-rc.N` and finally `0.1.0` through the configured branches. After the first tag, derive minor and patch progression from tag history and use the approved explicit major-increment mechanism when the product is ready to enter the `1.0.0` line.

Implement tag-driven version calculation using GitVersion. The source tag must produce one consistent pre-release version across Host assemblies, `--version` output, the .NET tool package, symbol packages and release notes. Reject or visibly fail builds whose source identity cannot be represented correctly.

**Completion evidence:** Automated tests or build inspection prove version consistency for a representative alpha tag and ordinary non-release builds.

Implementation evidence (2026-09-01):

- Pinned `GitVersion.Tool` 6.8.2 in the repository manifest and validated the configuration in read-only no-cache mode. On `develop`, GitVersion produced `SemVer` and `FullSemVer` `0.1.0-beta.282`, numeric assembly versions `0.1.0.0`, commit `5e903042f66e4c399ab500c67058b90b9d2cf92d` and source distance 282.
- Verified an ordinary build and direct Host execution use `0.0.0-dev`, while a deliberately incomplete release build fails before compilation rather than silently retaining the development identity.
- Built representative alpha packages with identity `0.1.0-alpha.1` and confirmed one-shot installed execution reported that exact version. The calculated beta package also confirmed that the package version, Host `--version`, `AssemblyInformationalVersion`, source tag and release URL all use `0.1.0-beta.282` without a `v` prefix. Generated assembly metadata retained the exact commit, `FullSemVer` and source distance separately, while `AssemblyVersion` and `FileVersion` were `0.1.0.0`.
- Added evaluated-build integration coverage that accepts valid release provenance while rejecting independent overrides of the derived package, informational, assembly and file identities, malformed or mismatched semantic versions, incomplete commit SHAs and invalid source distances.

### `ALPHA-010` Finalise package and executable metadata

**Status:** Complete

Approved decisions:

- Use `Roslyn.Workbench.Mcp` as the Host package ID and assembly/executable name and `roslyn-workbench-mcp` as the installed .NET tool command. Use the same package ID for alpha, beta, RC and production releases, distinguishing channels through `SemVer` rather than `.Tool`, `.Cli` or channel-specific package names. Recheck package ownership and availability immediately before first publication.
- Use **Roslyn Workbench MCP** as the package title; “A local MCP server for Roslyn-powered C# code analysis and safe, transactional refactoring.” as its description; **Lantean Code** as author and company; `Copyright © 2026 Lantean Code`; the `MIT` licence expression; the GitHub Pages root as project URL; `https://github.com/lantean-code/roslyn-workbench-mcp` as the `git` repository URL; and the exact source commit as package repository metadata.
- Derive `PackageReadmeFile` from the release-ready root `README.md`, with channel-aware GitHub Packages or NuGet.org installation guidance and links to the matching versioned documentation rather than a duplicated manual. Replace the repository-relative wordmark reference in the packaged copy with an immutable `raw.githubusercontent.com` URL for the exact source tag because NuGet.org does not render relative README images. Do not otherwise maintain a separate package narrative. Include `LICENSE`, `THIRD-PARTY-NOTICES.md` and the required third-party licence texts in the Host package. Exclude development plans, audits, worklists and contributor-only documentation.
- Use `assets/roslyn-workbench-mcp-icon.svg` as the locked source for package and application icon derivatives. Generate a deterministic 128×128 PNG without redrawing, cropping, recolouring or changing its proportions, embed that PNG as the Host package's `PackageIcon`, and inspect it in package metadata. Retain the source SVG and do not pass it directly to NuGet because NuGet package icons support PNG and JPEG rather than SVG.
- Use the package tags `model-context-protocol`, `mcp`, `mcp-server`, `roslyn`, `csharp`, `dotnet`, `code-analysis`, `refactoring` and `developer-tools`. Do not add broad `ai`, `agent` or `visual-studio` tags that would attract unrelated searches or imply unsupported integration.
- Produce one validated, versioned Host build containing the Host and project assemblies, bundled Core plugin, Code Action and Workspace assemblies, runtime dependencies, `.deps.json`, `.runtimeconfig.json`, portable PDBs and legal files, then assemble distribution-specific wrappers from that output without recompiling or changing it. For alpha, create only a framework-dependent `net10.0` .NET tool package containing the complete runtime payload and no floating downstream implementation dependency ranges; do not trim, single-file publish or self-contain it. Preserve the packaging boundary so later MSI and Debian/apt packages can consume the same release build and Chocolatey and WinGet can normally distribute the approved MSI. Decide whether native installers remain framework-dependent or carry a runtime during their v1 design rather than constraining that choice in alpha.
- Produce deterministic portable PDBs with Source Link for the exact repository commit and include them in the Host runtime payload so first-party diagnostic frames retain line information. Generate a matching `.snupkg` for every release package. Publish it to NuGet.org's symbol server when the beta, RC or production package is published there; for alpha/beta packages confined to GitHub Packages, retain the PDBs in the tool payload and attach the `.snupkg` to the GitHub prerelease rather than treating GitHub Packages as a symbol server. Require the `.nupkg` and `.snupkg` to share the exact package ID and `SemVer`.
- Do not establish a NuGet CLR public-API compatibility baseline for the alpha Host package because it is an executable tool rather than a supported library. Treat MCP contracts, configuration, persisted state, command-line behaviour and documented tool behaviour as the applicable compatibility surfaces. Validate package metadata and contents, `.nuspec`, deterministic source identity, dependency closure, global and manifest installation, `dnx` execution where supported, update, removal and published-Host operation. Run package checks that apply to .NET tools and suppress library-specific API compatibility checks with an explicit rationale. Defer CLR API compatibility validation to the supported Plugins package planned for v1.
- Set `PackageReleaseNotes` to the immutable tag-derived GitHub Release URL `https://github.com/lantean-code/roslyn-workbench-mcp/releases/tag/{SemVer}` and keep the full notes in that release and the matching versioned documentation rather than duplicating them in package metadata. Validate that the rendered notes exist before publication and use the same URL for GitHub Packages and NuGet.org. When a temporary alpha/beta release is removed under the approved cleanup policy, remove its GitHub Package version at the same time so no retained package points to a deleted release page.

Review package identifiers, titles, descriptions, authors, licence, repository URL and commit metadata, project URL, readmes, icons, tags, release notes, dependency ranges, symbol-package settings, package validation and the intended alpha public-API compatibility baseline. Confirm the .NET tool command and package names are predictable and do not collide with stable channels. Ensure every package identifies itself as pre-release where the ecosystem supports it.

**Completion evidence:** The generated packages, `.nuspec` files and assemblies pass a recorded metadata inspection.

Implementation evidence (2026-09-01):

- Generated and inspected `Roslyn.Workbench.Mcp.0.1.0-beta.282.nupkg` and its matching `.snupkg`. The tool package used the approved ID, title, command, description, author, company, copyright, licence, project and repository metadata, tags and immutable release URL; its generated README replaced the repository-relative wordmark with the exact source-tag URL.
- Confirmed the package contains the complete framework-dependent `net10.0` runtime payload without NuGet dependency ranges, including all first-party runtime assemblies, bundled Core plugin, Code Actions, Workspace, `.deps.json`, `.runtimeconfig.json`, portable PDBs and legal files. The embedded 128×128 icon hash exactly matched the locked asset, and Source Link mapped first-party PDB documents to the exact commit.
- Validated isolated global installation, exact-version execution, update from `0.0.0-dev`, manifest installation and execution, one-shot `dotnet tool exec`/`dnx` execution, manifest removal and global removal. Every representative release execution reported `0.1.0-beta.282`.
- The Host test project passed all 649 tests; the new `HostCommandLine` implementation recorded 100% line and branch coverage. The complete Host integration suite passed all 150 tests, including evaluated release-identity validation and process-level verification that `--version` writes only the exact informational version. The full solution build completed without warnings or errors; the affected `latest-all` analyzer build retained only three existing fixture-only `CA1812` diagnostics outside the changed files.

### `ALPHA-011` Implement manually triggered release builds

**Status:** Design approved

Approved decisions:

- Start alpha, beta and RC release builds manually from GitHub Actions using GitHub's normal branch selector. Build the exact selected commit, derive the channel from its branch through GitVersion, and publish it through the destination approved for that release.
- Ordinary pull requests, merges and pushes may run continuous integration, but they do not create packages intended for distribution or GitHub Releases. A release happens only through the manual release workflow, except that the production workflow starts from the stable tag created on the `main` merge commit.
- A release run restores, builds, analyses, tests and packages the source once. It should retain the .NET tool package, matching symbol package, checksums, source and version details, release notes and useful validation results so a published build can be understood and investigated later.
- Route publication according to `ALPHA-009`: GitHub Packages by default for alpha and beta, and NuGet.org for RC and production, with an explicitly approved release allowed to put a selected beta on NuGet.org. Do not reuse a version already published to NuGet.org.
- Pin actions to reviewed commit SHAs and give each job only the permissions it needs. Publication credentials must remain unavailable to pull-request and ordinary continuous-integration jobs.
- For a prerelease, create the calculated `SemVer` tag on the exact selected commit as part of creating the GitHub prerelease. For production, build from the stable tag created on the `main` merge commit through the approved GitFlow sequence. Use that source identity for assemblies, packages, documentation and release metadata.
- Base reproducibility on the exact source commit, tag, pinned .NET SDK and centrally managed exact direct dependency versions. Do not add `packages.lock.json` files solely for release automation or caching, and leave NuGet package caching disabled unless measured restore performance justifies it.

The workflow should make the intended action obvious, report failed checks clearly and leave enough evidence to diagnose a bad release. It need not model a separate unpublished candidate or formal promotion record before a small-project release can proceed.

**Completion evidence:** A manually dispatched prerelease produces and publishes the expected package and release information from an allowed branch, while pull-request, merge and ordinary push runs cannot publish a package or create a GitHub Release.

### `ALPHA-012` Record manual scenario and performance analysis

**Status:** Design approved

Approved decisions:

- Keep the external-repository scenario runner outside GitHub Actions. Its workloads are too intensive for routine hosted automation and primarily provide engineering and performance analysis rather than a release pass/fail result.
- Run the scenarios manually against the selected alpha build, or against a later release branch or published RC, when their evidence would help assess the build. Do not make completion of a scenario run an automated publication requirement.
- Emit a versioned normalised aggregate and scenario-suite identity, retain the detailed output needed for investigation, and produce an advisory comparison with the preceding comparable release when one exists.
- Treat timing and resource changes as engineering evidence to investigate. Do not introduce fixed performance thresholds until repeated comparable runs establish normal variance and a demonstrated product need justifies them.
- Run on the locally available supported Windows, Linux or WSL environments appropriate to the investigation. Best-effort macOS evidence may be collected when suitable infrastructure is available, without making it an alpha support requirement.

**Completion evidence:** A documented manual run against a representative alpha build records its source and scenario-suite identities, retains understandable results and provides a useful comparison where preceding evidence exists.

**Sources:** [`TestingStrategy.md`](TestingStrategy.md#release-validation-and-performance-history), [`AcceptanceCoverageAudit-2026-07-23.md`](AcceptanceCoverageAudit-2026-07-23.md#release-only-scenario-validation-and-metrics)

### `ALPHA-013` Configure protected publication

**Status:** Design approved

Approved decisions:

- Use the release workflow's scoped `GITHUB_TOKEN` to publish repository-associated packages to GitHub Packages. Do not store a personal access token for that publication path.
- Use NuGet.org trusted publishing with short-lived OIDC credentials when it is available to the package owner. Fall back to a narrowly scoped NuGet API key only when trusted publishing cannot be configured, and retain it only as an Actions secret available to the publication job.
- Treat the deliberately started prerelease workflow or deliberately created production tag as the maintainer's publication approval. Do not add a second environment-review prompt solely to model another approval gate.
- Grant package, release and OIDC write permissions only to the job that needs them. Pull-request and ordinary continuous-integration jobs remain read-only and cannot access publication credentials.
- Restrict each publication path to its approved branch or tag shapes and verify the calculated version and destination before pushing. Keep package ownership and recovery access with the project owner.
- Verify the authentication setup without publishing a real package where the service permits it. If a harmless authentication-only check is not available, make the first explicitly approved prerelease the end-to-end validation and stop clearly before any fallback retry or destination change.

**Completion evidence:** The configured publication job can authenticate through the intended short-lived or scoped identity, while pull-request and ordinary continuous-integration jobs cannot publish or obtain its credentials.

## Stage 4 — Manual alpha checks and documentation

### `ALPHA-017` Complete security, dependency and provenance checks

**Status:** Design approved

Approved decisions:

- Before publishing the alpha, check that GitHub and the relevant package registry report no unresolved critical or high-severity dependency vulnerability affecting the release.
- Check that the release workflow, generated package and attached files contain no credentials or unintended machine-specific data.
- Confirm that package source metadata, commit identity and published checksums identify the release being built.
- Consider lower-severity findings and known limitations in context. Do not require a formal written disposition, security report or compliance record for every non-blocking item.
- Rely on the repository's normal dependency alerts, source review and protected publication design rather than duplicating them in a separate release audit.

**Completion evidence:** The maintainer has checked the release's significant alerts, credential exposure and source identity and found no issue that should prevent publication.

### `ALPHA-018` Complete release documentation and notes

**Status:** Design approved

Approved decisions:

- Confirm that the existing installation, configuration, troubleshooting, security and removal documentation matches the package and behaviour being released. Update those pages rather than creating a separate release manual.
- State that the alpha does not publish plugin-authoring guidance or the Plugins NuGet package, without claiming that the existing runtime rejects source-built plugins.
- Prepare release notes covering the alpha status, notable capabilities, supported platforms, known limitations, compatibility expectations and feedback routes. Approve the exact wording while preparing the release rather than fixing it during this design pass.
- Verify public, package-contained and versioned documentation links against the published site and package destinations.
- Remove temporary development documentation before the repository goes live, including worklists, audits, review evidence, dogfood tracking and superseded design records under `docs/development`. These records are useful while preparing the product but should not remain as post-release project documentation.
- Before removing a development record, move or rewrite any information that remains genuinely useful for users or future maintainers into the appropriate permanent product, architecture or contributor documentation. Do not preserve temporary tracking documents merely as project history.

**Completion evidence:** Release-facing documentation and notes describe the shipped product, all public links work, and temporary development tracking has been removed after any lasting guidance has been preserved appropriately.

## First-alpha completion criteria

The first alpha is complete only when:

- the public artifact version and source identity are consistent and reproducible;
- each published version is traceable to its exact source commit and release workflow;
- clean users can follow public installation and usage documentation without repository project references or build output;
- supported Windows and Linux paths have been validated against a published release build, with any best-effort platform evidence clearly labelled;
- GitHub Issues, Discussions and private security reporting route users to the correct maintainable destination;
- no unresolved critical or high-severity functionality or security finding remains;
- known limitations and pre-release compatibility expectations are prominent; and
- any issue discovered during manual alpha checks has been fixed or added to `FutureTasks.md` with enough context to act on it.
