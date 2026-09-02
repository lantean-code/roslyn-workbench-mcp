# Security Policy

## Supported versions

Security fixes target the latest production release, or the latest published prerelease when no production release exists. Reports affecting prereleases and older versions are welcome, but fixes are not backported to older release lines. No response or remediation time is promised.

## Report a vulnerability

Do not disclose a suspected vulnerability through a public Issue, Discussion or pull request.

Use [GitHub private vulnerability reporting](https://github.com/lantean-code/roslyn-workbench-mcp/security/advisories/new) when it is available. Include the affected version, operating system, workspace or plugin boundary, reproduction steps, expected impact and any suggested mitigation. Share only the minimum evidence necessary to investigate the problem.

If private vulnerability reporting is unavailable, email `lanteancode@gmail.com`. Do not send source code, credentials, private keys, access tokens, unnecessary paths or other secrets by email. Begin with a concise description so a safer exchange can be arranged if more evidence is required.

Confirmed vulnerabilities are coordinated privately. A GitHub Security Advisory will be published when an issue materially affects released users.

## Security boundaries

Roslyn Workbench is a local stdio process that reads and may transactionally modify source files selected by its caller. Review staged changes before committing them and restrict access to its state directory.

Workspaces are executable inputs rather than passive source trees. `workspace-open` evaluates MSBuild project logic, including repository-controlled projects and imports, before an agent can inspect every input. Later diagnostic and Code Action operations can load and execute project analyzers with the Host's operating-system permissions. The Host does not sandbox workspace build logic or analyzers. Open only workspaces whose source, project files, imported build logic, SDK configuration and analyzer dependencies are fully trusted; inspect an untrusted repository outside Roslyn Workbench or in an operating-system sandbox first.

Third-party plugins execute as trusted in-process code with the Host's operating-system permissions. They are not sandboxed and can access files, processes, network resources and Roslyn workspace objects. Do not load a plugin unless its code and dependencies are fully trusted.
