# Security Policy

## Supported versions

Security fixes are applied to the latest supported Roslyn Workbench release. Pre-release builds and older versions may be used to reproduce a report but do not receive separate long-term support.

## Reporting a vulnerability

Do not disclose a suspected vulnerability through a public issue, discussion or pull request.

Use the repository's **Security** tab to submit a private vulnerability report. Include the affected version, operating system, workspace or plugin boundary, reproduction steps, expected impact and any suggested mitigation. Remove source code, credentials and other sensitive data that are not necessary to reproduce the issue.

If private vulnerability reporting is unavailable, contact the maintainers privately before sharing details.

## Security boundaries

Roslyn Workbench is a local stdio process that reads and may transactionally modify source files selected by its caller. Review staged changes before committing them and restrict access to its state directory.

Workspaces are executable inputs rather than passive source trees. `workspace-open` evaluates MSBuild project logic, including repository-controlled projects and imports, before an agent can inspect every input. Later diagnostic and Code Action operations can load and execute project analyzers with the Host's operating system permissions. The Host does not sandbox workspace build logic or analyzers. Open only workspaces whose source, project files, imported build logic, SDK configuration and analyzer dependencies are fully trusted; inspect an untrusted repository outside Roslyn Workbench or in an operating-system sandbox first.

Third-party plugins execute as trusted in-process code with the Host's operating system permissions. They are not sandboxed and can access files, processes, network resources and Roslyn workspace objects. Do not load a plugin unless its code and dependencies are fully trusted.
