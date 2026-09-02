# Issue Triage

Repository maintainers review new Issues at least weekly and before each release decision. This cadence does not promise an individual response or resolution time.

## New reports

1. Check that the report belongs in a public Issue and does not disclose a vulnerability, personal data, credentials, private source or other sensitive material. Remove exposed material where possible and move security handling to private vulnerability reporting.
2. Confirm the issue type, affected areas and platforms. Replace `status/needs-triage` with the appropriate reviewed status and replace `type/bug` with `type/performance` when a Bug report describes a performance regression.
3. Request missing information or a reproduction explicitly. Apply `needs/information` or `needs/reproduction` while waiting.
4. Close after 14 days without the requested response, explaining that the reporter may ask to reopen the Issue when the evidence is available.

## Lifecycle and resolution

- `status/confirmed` means a defect has been reproduced.
- `status/accepted` means work is approved in principle; it does not promise scheduling.
- `status/in-progress` means implementation has started.
- `status/blocked` identifies accepted or active work that cannot currently proceed. Use a matching `needs/*` label for the immediate blocker when applicable.
- Link duplicates to one canonical Issue and close them with `resolution/duplicate`.
- Use `resolution/cannot-reproduce` after reasonable reproduction attempts and explain what evidence could change the decision.
- Explain `resolution/not-planned` decisions concisely. Deferral is not the same as rejection; link the owning work item when work is deliberately deferred.

The project does not use an automatic stale-issue bot. Release planning is maintainer-led; no milestone or GitHub Project is required.

## Priority

Maintainers assign at most one priority according to impact and urgency, never effort or comment volume:

- `priority/critical`: release-blocking data loss, unsafe writes, security impact or inability to install or start on a supported platform.
- `priority/high`: an unusable major workflow without a reasonable workaround, or prohibitive performance or resource impact.
- `priority/normal`: confirmed limited or tolerably degraded behaviour with a workaround.
- `priority/low`: minor inconvenience, polish, low-impact documentation or speculative enhancement.

Do not expose private vulnerability severity through public Issue labels. Each Issue has at most one `type/*`, `status/*` and `priority/*` label, but may have multiple `area/*`, `platform/*` and temporary `needs/*` labels.
