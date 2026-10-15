#!/usr/bin/env python3
"""Capture live MCP catalogues and report deterministic TDQS findings without a model."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import subprocess
import sys
import tempfile
from importlib.metadata import version
from pathlib import Path

import anyio
from mcp.client import Client
from mcp.client.stdio import StdioServerParameters, stdio_client
from mcp.types import Implementation
from tdqs import format_lint_report_markdown
from tdqs.lint_report import LintReport


OPERATIONAL_MODES = (
    "inspection-only", "transactional", "approval-required", "autonomous-trusted",
)
OUTPUT_SCHEMA_MODES = ("Omit", "Full")


def write_json(path: Path, value: dict) -> None:
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


async def capture_catalogue(host: Path, mode: str, schema_mode: str, log: Path) -> dict:
    # A fresh state directory prevents recovery of unrelated user transactions. The SDK's
    # default environment excludes local Host/plugin configuration and model credentials.
    with tempfile.TemporaryDirectory(prefix="roslyn-tool-quality-") as state:
        parameters = StdioServerParameters(
            command="dotnet",
            args=[
                str(host), "--operational-mode", mode,
                "--tool-output-schema-mode", schema_mode,
                "--error-reporting-consent", "prompt", "--state-directory", state,
            ],
            cwd=state,
        )
        with log.open("w", encoding="utf-8") as stderr, anyio.fail_after(60):
            async with Client(
                stdio_client(parameters, errlog=stderr),
                client_info=Implementation(name="roslyn-tool-quality", version="1"),
                read_timeout_seconds=30,
            ) as client:
                tools = []
                cursor = None
                seen_cursors = set()
                while True:
                    page = await client.list_tools(cursor=cursor)
                    tools.extend(
                        tool.model_dump(mode="json", by_alias=True, exclude_unset=True)
                        for tool in page.tools
                    )
                    cursor = page.next_cursor
                    if cursor is None:
                        break
                    if cursor in seen_cursors:
                        raise ValueError("The Host repeated a tools/list pagination cursor.")
                    seen_cursors.add(cursor)

                return {"serverName": client.server_info.name, "tools": tools}


def lint_catalogue(directory: Path) -> tuple[int, LintReport]:
    result = subprocess.run(
        [
            sys.executable, "-m", "tdqs", "lint",
            "--file", str(directory / "catalogue.json"),
            "--fail-on", "error", "--format", "json",
            "--output", str(directory / "lint.json"),
        ],
        capture_output=True, text=True, encoding="utf-8", timeout=60, check=False,
    )
    if result.returncode not in (0, 1):
        raise RuntimeError(f"TDQS failed (exit {result.returncode}): {result.stderr.strip()}")
    report = LintReport.model_validate_json((directory / "lint.json").read_text(encoding="utf-8"))
    (directory / "lint.md").write_text(format_lint_report_markdown(report), encoding="utf-8")
    return result.returncode, report


def summarise_profile(profile: str, report: LintReport) -> str:
    counts = {
        severity: sum(finding.severity == severity for finding in report.findings)
        for severity in ("error", "warning", "info")
    }
    return (
        f"| {profile} | {report.server.tool_count} | {counts['error']} | "
        f"{counts['warning']} | {counts['info']} |"
    )


def describe_error(error: BaseException) -> str:
    if isinstance(error, BaseExceptionGroup):
        return "; ".join(describe_error(inner) for inner in error.exceptions)
    return str(error) or type(error).__name__


def generate_reports(host: Path, output: Path) -> int:
    output.mkdir(parents=True, exist_ok=True)
    metadata = {
        "hostSha256": hashlib.sha256(host.read_bytes()).hexdigest(),
        "sourceRevision": os.environ.get("GITHUB_SHA"),
        "tdqsVersion": version("tdqs"),
        "errorReportingConsent": "prompt",
        "externalPluginsEnabled": False,
    }
    write_json(output / "run.json", metadata)
    rows = []
    details = []
    exit_code = 0
    for mode in OPERATIONAL_MODES:
        for schema_mode in OUTPUT_SCHEMA_MODES:
            profile = f"{mode}-{schema_mode.lower()}"
            directory = output / profile
            directory.mkdir(exist_ok=True)
            # Do not publish a previous run's report if this capture fails.
            for name in ("catalogue.json", "lint.json", "lint.md", "failure.txt", "host.log"):
                (directory / name).unlink(missing_ok=True)
            try:
                catalogue = anyio.run(capture_catalogue, host, mode, schema_mode, directory / "host.log")
                write_json(directory / "catalogue.json", catalogue)
                status, report = lint_catalogue(directory)
                exit_code = max(exit_code, status)
                rows.append(summarise_profile(profile, report))
                markdown = (directory / "lint.md").read_text(encoding="utf-8")
                details.append(f"<details>\n<summary>{profile}</summary>\n\n{markdown}\n</details>\n")
                print(rows[-1], flush=True)
            except Exception as error:
                message = describe_error(error)
                (directory / "failure.txt").write_text(message + "\n", encoding="utf-8")
                rows.append(f"| {profile} | Capture/lint failed; see failure.txt and host.log | — | — | — |")
                print(f"{profile}: {message}", file=sys.stderr, flush=True)
                exit_code = 2

    summary = "\n".join([
        "# MCP tool definition lint", "",
        f"TDQS {metadata['tdqsVersion']}; offline lint only. Errors fail this check; warnings are advisory.",
        "Reporting tools are included (`prompt` consent); external plugins are disabled. No tools are invoked.",
        "Each profile uses its own live catalogue. Download the tool-quality artefact for captured JSON and Host logs.",
        "", "| Operational mode / output schemas | Tools | Errors | Warnings | Notes |",
        "| --- | ---: | ---: | ---: | ---: |", *rows, "", *details,
    ])
    (output / "summary.md").write_text(summary + "\n", encoding="utf-8")
    return exit_code


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host", type=Path, required=True, help="Built Roslyn.Workbench.Mcp.dll")
    parser.add_argument("--output", type=Path, default=Path("artifacts/tool-quality"))
    args = parser.parse_args()
    host = args.host.resolve()
    if not host.is_file():
        parser.error(f"Host assembly does not exist: {host}. Build the Host first.")
    return generate_reports(host, args.output.resolve())


if __name__ == "__main__":
    sys.exit(main())
