#!/usr/bin/env python3
"""Resolve and validate one release workflow invocation."""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path


_COMMIT_PATTERN = re.compile(r"^[0-9a-f]{40}$")
_SUPPORTED_DESTINATIONS = {"default", "github-packages", "nuget-org"}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--gitversion-json", required=True, type=Path)
    parser.add_argument("--event-name", required=True)
    parser.add_argument("--ref", required=True)
    parser.add_argument("--ref-name", required=True)
    parser.add_argument("--ref-type", required=True)
    parser.add_argument("--requested-destination", default="default")
    parser.add_argument("--requested-publish", default="false")
    parser.add_argument("--github-output", required=True, type=Path)
    parser.add_argument("--manifest", required=True, type=Path)
    arguments = parser.parse_args()

    git_version = read_git_version(arguments.gitversion_json)
    version = required_string(git_version, "SemVer")
    full_semver = required_string(git_version, "FullSemVer")
    commit = required_string(git_version, "Sha").lower()
    source_distance = required_non_negative_integer(git_version, "CommitsSinceVersionSource")

    if full_semver.split("+", 1)[0] != version:
        raise ValueError("GitVersion FullSemVer must have SemVer as its public identity.")
    if not _COMMIT_PATTERN.fullmatch(commit):
        raise ValueError("GitVersion Sha must be a complete 40-character commit SHA.")

    channel, publish = resolve_channel_and_publish(arguments, version)
    destination = resolve_destination(channel, arguments.requested_destination)
    prerelease = channel != "stable"
    release_notes_path = f"docs/content/releases/{version}.md"

    outputs = {
        "channel": channel,
        "commit": commit,
        "destination": destination,
        "full-semver": full_semver,
        "prerelease": lower_boolean(prerelease),
        "publish": lower_boolean(publish),
        "release-notes-path": release_notes_path,
        "source-distance": str(source_distance),
        "version": version,
    }
    write_github_outputs(arguments.github_output, outputs)
    write_manifest(
        arguments.manifest,
        version,
        full_semver,
        commit,
        source_distance,
        channel,
        destination,
    )
    return 0


def read_git_version(path: Path) -> dict[str, object]:
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError("GitVersion output must be a JSON object.")
    return value


def required_string(values: dict[str, object], key: str) -> str:
    value = values.get(key)
    if not isinstance(value, str) or not value:
        raise ValueError(f"GitVersion output is missing '{key}'.")
    return value


def required_non_negative_integer(values: dict[str, object], key: str) -> int:
    value = values.get(key)
    if not isinstance(value, int) or isinstance(value, bool) or value < 0:
        raise ValueError(f"GitVersion output '{key}' must be a non-negative integer.")
    return value


def resolve_channel_and_publish(arguments: argparse.Namespace, version: str) -> tuple[str, bool]:
    if arguments.event_name != "workflow_dispatch":
        raise ValueError("Release runs must be started manually.")

    publish = parse_boolean(arguments.requested_publish)
    if arguments.ref_type == "tag":
        if arguments.ref != f"refs/tags/{arguments.ref_name}":
            raise ValueError("A production release must use an exact tag ref.")
        if arguments.ref_name != version or "-" in version:
            raise ValueError("A production tag must exactly equal the stable GitVersion SemVer.")
        return "stable", publish

    if arguments.ref_type != "branch":
        raise ValueError("Prerelease runs must be manually dispatched from an allowed branch.")
    if arguments.ref != f"refs/heads/{arguments.ref_name}":
        raise ValueError("A prerelease must use an exact branch ref.")

    channel = channel_for_branch(arguments.ref_name)
    require_channel_version(version, channel)
    return channel, publish


def channel_for_branch(branch: str) -> str:
    if branch.startswith("feature/"):
        return "alpha"
    if branch == "develop":
        return "beta"
    if branch.startswith("release/") or branch.startswith("hotfix/"):
        return "rc"
    raise ValueError("Manual releases are supported only from feature/*, develop, release/* or hotfix/* branches.")


def require_channel_version(version: str, channel: str) -> None:
    expected_prefix = f"-{channel}."
    if expected_prefix not in version:
        raise ValueError(f"GitVersion SemVer '{version}' does not represent the '{channel}' channel.")


def resolve_destination(channel: str, requested_destination: str) -> str:
    if requested_destination not in _SUPPORTED_DESTINATIONS:
        raise ValueError(f"Unsupported publication destination '{requested_destination}'.")

    default_destination = "github-packages" if channel in {"alpha", "beta"} else "nuget-org"
    destination = default_destination if requested_destination == "default" else requested_destination
    allowed_destinations = {
        "alpha": {"github-packages"},
        "beta": {"github-packages", "nuget-org"},
        "rc": {"nuget-org"},
        "stable": {"nuget-org"},
    }
    if destination not in allowed_destinations[channel]:
        raise ValueError(f"The '{channel}' channel cannot publish to '{destination}'.")
    return destination


def parse_boolean(value: str) -> bool:
    if value == "true":
        return True
    if value == "false":
        return False
    raise ValueError(f"Expected 'true' or 'false', received '{value}'.")


def lower_boolean(value: bool) -> str:
    return "true" if value else "false"


def write_github_outputs(path: Path, outputs: dict[str, str]) -> None:
    with path.open("a", encoding="utf-8", newline="\n") as output_file:
        for key, value in outputs.items():
            output_file.write(f"{key}={value}\n")


def write_manifest(
    path: Path,
    version: str,
    full_semver: str,
    commit: str,
    source_distance: int,
    channel: str,
    destination: str,
) -> None:
    manifest = {
        "schemaVersion": 1,
        "packageId": "Roslyn.Workbench.Mcp",
        "version": version,
        "fullSemVer": full_semver,
        "commit": commit,
        "sourceDistance": source_distance,
        "channel": channel,
        "destination": destination,
    }
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8", newline="\n")


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError) as exception:
        print(f"Release configuration error: {exception}", file=sys.stderr)
        raise SystemExit(2) from None
