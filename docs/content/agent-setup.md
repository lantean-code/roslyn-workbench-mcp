# Agent setup

Connecting Roslyn Workbench to an MCP client makes its tools available, but it does not guarantee that the agent will choose them. An agent may otherwise use text search for a question that depends on symbol identity or edit source directly when a compiler-aware Code Action is available. A short repository instruction makes the intended division of responsibility explicit.

This guidance is optional. Roslyn Workbench remains compatible with any MCP client that can launch a local stdio server, and the running server's tool descriptions, schemas and structured next actions remain authoritative.

## Recommended instruction

The following text can be added to the repository guidance already used by your agent:

```markdown
## Roslyn Workbench

When Roslyn Workbench MCP is available, consider using it for C# work that benefits from compiler semantics, including precise symbol navigation, references, diagnostics, code structure, Code Actions, change-impact analysis and transactional source changes. Its semantic tools are particularly useful when symbol identity or compiler interpretation matters, while the repository's normal tools and commands remain suitable for builds, tests, package management, documentation and non-semantic file operations. Transaction previews and structured next actions can help keep source changes safe and easy to review.
```

This guidance is most useful when it highlights the work for which Roslyn Workbench adds value rather than treating it as a requirement for every .NET task. It complements the .NET CLI, the repository's test runner and ordinary file operations. Existing build, test, review and coding guidance can remain alongside it.

The instruction can be configured once at user level when Roslyn Workbench is available across repositories. Repository guidance forms a later, more specific layer and may be useful when the server is available only for that codebase or when a team wants to share the recommendation. In either scope, the phrase **when Roslyn Workbench MCP is available** helps agents work naturally when it is not installed.

## User-level setup

User-level guidance is a convenient starting point when Roslyn Workbench is configured for all applicable sessions. It keeps the recommendation out of individual repositories and makes it available whenever the agent encounters C# work.

### Codex

Codex can load the recommended instruction from `~/.codex/AGENTS.md`. This is the standard global guidance mechanism and can sit alongside other preferences that apply across repositories.

The same guidance can instead be included in `~/.codex/config.toml` using `developer_instructions`, which may be convenient when the Roslyn Workbench MCP server is also configured there:

```toml
developer_instructions = """
When Roslyn Workbench MCP is available, consider using it for C# work that benefits from compiler semantics, including precise symbol navigation, references, diagnostics, code structure, Code Actions, change-impact analysis and transactional source changes. Its semantic tools are particularly useful when symbol identity or compiler interpretation matters, while the repository's normal tools and commands remain suitable for builds, tests, package management, documentation and non-semantic file operations. Transaction previews and structured next actions can help keep source changes safe and easy to review.
"""
```

Where `developer_instructions` already contains other guidance, this text can be incorporated into the existing value rather than adding a second key.

Codex loads repository guidance from `AGENTS.md` before it starts work. See the [Codex AGENTS.md documentation](https://learn.chatgpt.com/docs/agent-configuration/agents-md) for instruction discovery, precedence and user-level alternatives.

### Claude Code

Claude Code can load the recommended instruction from `~/.claude/CLAUDE.md`, where it can sit alongside other user preferences shared across projects.

Claude Code uses `CLAUDE.md` for shared project instructions and can apply more specific files within subdirectories. See the [Claude Code memory documentation](https://docs.anthropic.com/en/docs/claude-code/memory) for discovery and import behaviour.

### Other MCP clients

Other clients may provide user rules, global project guidance or persistent system instructions. A user-level location is a natural fit when Roslyn Workbench is available across the repositories opened by that client.

## Repository-level setup

Repository guidance is loaded in addition to user-level guidance and gives an agent more specific context for the codebase it is currently working with. The recommended instruction can be included here instead of, or alongside, user-level setup.

### Codex

The recommended instruction can be added to the repository's root `AGENTS.md`. Where that file already exists, the section can sit alongside its existing instructions. More specific `AGENTS.md` files may refine the guidance for individual directories.

### Claude Code

The recommended instruction can be added to the repository's root `CLAUDE.md`. Where that file already exists, the section can sit alongside its existing instructions.

### Other MCP clients

The recommended text can be placed in the client's repository instructions or project rules using the equivalent project-level setting.

Clients differ in how they combine MCP metadata with their own instructions and how readily they select tools. When a client chooses a less suitable workflow, a direct request to use Roslyn Workbench for that task can help. Sharing the client and scenario through the project's support routes can also help improve future guidance.

## Detailed agent guidance

The [Agent guide](agent/index.md) explains trusted workspace loading, tool discovery, snapshot handling, transaction workflow and failure recovery. Keeping repository guidance concise avoids copying that detailed material into every agent session.
