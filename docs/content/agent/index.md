# Agent guide

The Roslyn Workbench skill provides guidance for semantic inspection, Code Actions, transactions and recovery. The running server's tool catalogue, schemas, structured results and next actions remain authoritative.

## Read the skill

Start with the [skill entry point](skill/SKILL.md), then load only the reference needed for the task. These pages and the download below use the same canonical skill files; the Markdown is also embedded in the Host.

| Task | Reference |
| --- | --- |
| Load, select, check freshness or reload a workspace | [Workspaces](skill/references/workspaces.md) |
| Reuse targets, snapshots and bounded results | [Selectors](skill/references/selectors.md) |
| Find symbols, references and calls | [Navigation](skill/references/navigation.md) |
| Inspect projects, configuration and symbol relationships | [Structure](skill/references/structure.md) |
| Investigate compiler and analyser findings | [Diagnostics](skill/references/diagnostics.md) |
| Analyse data and control flow | [Flow analysis](skill/references/flow-analysis.md) |
| Assess dependencies, change impact and cleanup candidates | [Impact analysis](skill/references/impact-analysis.md) |
| Stage, review, validate and persist changes | [Transactions](skill/references/transactions.md) |
| Discover and stage fixes, refactorings and Fix All | [Code Actions](skill/references/code-actions.md) |
| Rename symbols or format source | [Rename and format](skill/references/rename-format.md) |
| Recover failures or prepare an error report | [Recovery](skill/references/recovery.md) |

## Download and install separately

[Download the complete Roslyn Workbench skill](../assets/generated/roslyn-workbench-skill.zip).

Select the documentation version matching the installed server before downloading. Development documentation supplies the current development skill; older server releases retain their own versioned guidance. Releases from before skill delivery was introduced do not contain a downloadable bundle.

Extract the archive and place the entire `roslyn-workbench` directory in the skill directory supported by your agent, retaining `SKILL.md` and its `references`, `agents` and `assets` subdirectories. The bundle includes client metadata and the Roslyn Workbench logo; clients decide which metadata they support. Consult your client's skill installation guidance for its supported user-level or repository-level location. Installation does not grant permissions or change Host policy. Restart or refresh the client's skill discovery if it requires that.

The bundle works without the repository checkout or an internet connection after download. Keep its files together; copying only `SKILL.md` loses the supporting workflow guidance. Update the bundle when updating the server.

## Read through MCP resources

Clients that support MCP resources can list and read `skill://roslyn-workbench/SKILL.md` from the connected Host and request its 11 referenced workflow files individually. Relative links resolve under the same `skill://roslyn-workbench/` URI. Client metadata and the logo are included in the separate download; MCP resources publish the Markdown guidance. The files are embedded in the installed Host and available offline in every operational mode without opening a workspace. Initial server instructions include the entry URI and this versioned web fallback.

This is standard MCP resource delivery. Client support determines whether an agent can discover and load resources; publishing a skill file does not install or activate a native client skill. Native MCP Skills extension discovery is not advertised by this server. Separate installation is useful when the client supports local skills but does not expose resources to its agent.

See [agent setup](../agent-setup.md) for repository instructions, the [tool reference](../reference/tools/index.md) for individual tool contracts, and [configuration](../configuration.md) for operational policies.
