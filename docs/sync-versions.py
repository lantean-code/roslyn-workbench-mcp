#!/usr/bin/env python3
"""Synchronise published documentation with the repository's remote tags."""

from __future__ import annotations

import argparse
import copy
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys


_prerelease = re.compile(r"(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)-(alpha|beta|rc)\.(0|[1-9][0-9]*)")
_production = re.compile(r"(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)")
_pages_branch = "refs/heads/gh-pages"


def git(repository: Path, *arguments: str) -> str:
    return subprocess.run(["git", *arguments], cwd=repository, check=True, capture_output=True, text=True).stdout.strip()


def remote_tags(repository: Path) -> set[str]:
    output = git(repository, "ls-remote", "--tags", "--refs", "origin")
    return {line.split("refs/tags/", 1)[1] for line in output.splitlines() if "refs/tags/" in line}


def read_versions(repository: Path, revision: str) -> dict[str, dict]:
    entries = json.loads(git(repository, "show", f"{revision}:versions.json"))
    if not isinstance(entries, list):
        raise ValueError("The documentation version inventory must be an array.")

    versions = {}
    for entry in entries:
        if not isinstance(entry, dict) or not isinstance(entry.get("version"), str) or not isinstance(entry.get("aliases"), list):
            raise ValueError("The documentation version inventory contains an invalid entry.")
        if not all(isinstance(alias, str) for alias in entry["aliases"]):
            raise ValueError("Every documentation alias must be a string.")

        name = entry["version"]
        if name in versions:
            raise ValueError(f"Duplicate documentation version: {name}.")
        if name != "dev" and _production.fullmatch(name) is None and _prerelease.fullmatch(name) is None:
            raise ValueError(f"Documentation version '{name}' is not a recognised release version.")
        if any(alias != "latest" for alias in entry["aliases"]):
            raise ValueError(f"Documentation version '{name}' carries an unsupported alias; inspect it manually.")

        versions[name] = entry

    if "dev" not in versions:
        raise ValueError("The documentation inventory must retain the development version.")
    return versions


def root_target(repository: Path, revision: str) -> str | None:
    if not git(repository, "ls-tree", "--name-only", revision, "--", "index.html"):
        return None

    html = git(repository, "show", f"{revision}:index.html")
    match = re.search(r'window\.location\.replace\(\s*"([^"/]+)/"', html)
    if match is None:
        raise ValueError("The documentation root is not a recognised Mike redirect; leave it unchanged for manual inspection.")

    target = match.group(1)
    if f'content="1; url={target}/"' not in html or f'href="{target}/"' not in html:
        raise ValueError("The documentation root contains inconsistent redirect targets.")
    return target


def production_order(version: str) -> tuple[int, int, int]:
    match = _production.fullmatch(version)
    if match is None:
        raise ValueError(f"'{version}' is not a production version.")
    return tuple(int(match.group(index)) for index in (1, 2, 3))


def prerelease_order(version: str) -> tuple[int, int, int, int]:
    match = _prerelease.fullmatch(version)
    if match is None:
        raise ValueError(f"'{version}' is not a prerelease version.")
    return tuple(int(match.group(index)) for index in (1, 2, 3, 5))


def desired_state(versions: dict[str, dict], tags: set[str]) -> tuple[dict[str, dict], list[str], str]:
    orphaned = sorted(name for name in versions if name != "dev" and name not in tags)
    remaining = {name: copy.deepcopy(entry) for name, entry in versions.items() if name not in orphaned}

    for entry in remaining.values():
        entry["aliases"] = [alias for alias in entry["aliases"] if alias != "latest"]

    production_versions = [name for name in remaining if _production.fullmatch(name)]
    if production_versions:
        latest = max(production_versions, key=production_order)
        remaining[latest]["aliases"].append("latest")
        return remaining, orphaned, "latest"

    beta_versions = [name for name in remaining if (match := _prerelease.fullmatch(name)) and match.group(4) == "beta"]
    if beta_versions:
        return remaining, orphaned, max(beta_versions, key=prerelease_order)

    return remaining, orphaned, "dev"


def run_mike(repository: Path, *arguments: str) -> None:
    beside_python = Path(sys.executable).with_name("mike")
    executable = str(beside_python) if beside_python.is_file() else shutil.which("mike")
    if executable is None:
        raise FileNotFoundError("Install the documentation dependencies so the Mike CLI is available.")

    subprocess.run(
        [executable, *arguments, "--config-file", str(repository / "docs/mkdocs.yml"), "--branch", "gh-pages"],
        cwd=repository,
        check=True,
    )


def validate_sync(
    repository: Path,
    baseline: str,
    original_versions: dict[str, dict],
    expected_versions: dict[str, dict],
    orphaned: list[str],
    expected_root: str,
) -> None:
    if read_versions(repository, _pages_branch) != expected_versions:
        raise ValueError("The synchronised documentation inventory does not match the tag-backed versions.")
    if root_target(repository, _pages_branch) != expected_root:
        raise ValueError("Documentation synchronisation did not set the expected root redirect.")

    for version in orphaned:
        if git(repository, "ls-tree", "--name-only", _pages_branch, "--", version):
            raise ValueError(f"Orphaned documentation directory '{version}' is still present.")

    allowed_prefixes = tuple(f"{version}/" for version in orphaned)
    latest_changed = any(
        entry["aliases"] != expected_versions[name]["aliases"]
        for name, entry in original_versions.items()
        if name in expected_versions
    ) or any("latest" in original_versions[name]["aliases"] for name in orphaned)
    for line in git(repository, "diff", "--no-renames", "--name-status", baseline, _pages_branch).splitlines():
        _, path = line.split("\t", 1)
        if path in {"versions.json", "index.html"}:
            continue
        if allowed_prefixes and path.startswith(allowed_prefixes):
            continue
        if latest_changed and (path == "latest" or path.startswith("latest/")):
            continue
        raise ValueError(f"Documentation synchronisation unexpectedly changed '{path}'; nothing will be pushed.")


def synchronise(repository: Path, push: bool) -> bool:
    if not git(repository, "ls-remote", "--heads", "origin", _pages_branch):
        print("No published documentation branch exists; nothing to synchronise.")
        return False

    git(repository, "fetch", "--no-tags", "origin", "+refs/heads/gh-pages:refs/remotes/origin/gh-pages")
    baseline = git(repository, "rev-parse", "refs/remotes/origin/gh-pages")
    versions = read_versions(repository, baseline)
    tags = remote_tags(repository)
    expected_versions, orphaned, expected_root = desired_state(versions, tags)
    original_root = root_target(repository, baseline)

    local = subprocess.run(["git", "rev-parse", "--verify", _pages_branch], cwd=repository, capture_output=True, text=True)
    previous = local.stdout.strip() if local.returncode == 0 else None
    if previous is not None and previous != baseline:
        raise ValueError("The local gh-pages branch differs from origin; use a fresh checkout rather than discarding local work.")
    if git(repository, "branch", "--show-current") == "gh-pages":
        raise ValueError("Run documentation synchronisation from a source checkout, not the generated gh-pages branch.")
    if previous is None:
        git(repository, "branch", "gh-pages", baseline)

    try:
        if orphaned:
            run_mike(repository, "delete", *orphaned)

        latest_version = next((name for name, entry in expected_versions.items() if "latest" in entry["aliases"]), None)
        current_latest = next((name for name, entry in versions.items() if "latest" in entry["aliases"] and name not in orphaned), None)
        if latest_version is not None and latest_version != current_latest:
            run_mike(repository, "alias", "--update-aliases", "--alias-type", "redirect", latest_version, "latest")

        if expected_root != original_root:
            run_mike(repository, "set-default", expected_root)

        validate_sync(repository, baseline, versions, expected_versions, orphaned, expected_root)
        if remote_tags(repository) != tags:
            raise ValueError("The remote tag set changed during documentation synchronisation; rerun against the new state.")

        if push:
            git(repository, "push", "origin", "gh-pages")
            git(repository, "fetch", "--no-tags", "origin", "+refs/heads/gh-pages:refs/remotes/origin/gh-pages")
            print(f"Synchronised documentation; removed {len(orphaned)} version(s): {', '.join(orphaned) or 'none'}.")
        else:
            print(f"Dry run validated documentation synchronisation; would remove: {', '.join(orphaned) or 'none'}.")
        return True
    finally:
        current = git(repository, "rev-parse", _pages_branch)
        if previous is None:
            git(repository, "update-ref", "-d", _pages_branch, current)
        else:
            git(repository, "update-ref", _pages_branch, previous, current)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--push", action="store_true", help="Publish validated synchronisation; otherwise perform a local dry run.")
    arguments = parser.parse_args()
    repository = Path(__file__).resolve().parent.parent
    ready = synchronise(repository, arguments.push)
    if output := os.environ.get("GITHUB_OUTPUT"):
        with open(output, "a", encoding="utf-8") as stream:
            stream.write(f"deploy={'true' if ready and arguments.push else 'false'}\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
