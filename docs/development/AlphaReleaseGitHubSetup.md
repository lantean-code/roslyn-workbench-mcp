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

## Community activation

- [ ] Create or update the labels from `.github/labels.yml` without introducing synonyms.
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

## Final public activation

Complete these steps in order as one explicitly approved activation sequence:

- [ ] Make the repository public.
- [ ] Immediately enable private vulnerability reporting, then verify that the public security policy and `https://github.com/lantean-code/roslyn-workbench-mcp/security/advisories/new` reach the private reporting form from an unauthenticated session.
- [ ] Verify the README wordmark, alpha warning, installation guidance, support routes, package links, release links and licence from an unauthenticated view.
- [ ] Verify GitHub recognises `CODE_OF_CONDUCT.md`, `CONTRIBUTING.md`, `SECURITY.md`, `SUPPORT.md` and the MIT licence as community health files.
- [ ] Confirm the public Pages site and all README/community links work without authentication.

Completion of the preceding checklist sections does not authorise the repository visibility change. Public activation still requires explicit approval.
