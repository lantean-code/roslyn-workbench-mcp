#!/usr/bin/env python3
"""Read a previous trusted coverage snapshot; never execute downloaded content."""

from __future__ import annotations

import argparse
import io
import json
import os
import subprocess
import zipfile
from pathlib import Path
from urllib.parse import urlencode


def github(path: str, binary: bool = False) -> bytes:
    command = ["gh", "api", path]
    if binary:
        command.extend(["-H", "Accept: application/octet-stream"])
    return subprocess.check_output(command, stderr=subprocess.PIPE, timeout=60)


def read_json(path: str):
    return json.loads(github(path))


def from_ci(repo: str, branch: str) -> bytes | None:
    query = urlencode({"branch": branch, "event": "push", "status": "success", "per_page": 20})
    runs = read_json(f"repos/{repo}/actions/workflows/tests.yml/runs?{query}")["workflow_runs"]
    for run in runs:
        if str(run["id"]) == os.environ.get("GITHUB_RUN_ID"):
            continue
        artifacts = read_json(f"repos/{repo}/actions/runs/{run['id']}/artifacts?per_page=100")["artifacts"]
        for artifact in artifacts:
            if artifact["name"] != "coverage-baseline" or artifact["expired"]:
                continue
            archive = github(f"repos/{repo}/actions/artifacts/{artifact['id']}/zip")
            with zipfile.ZipFile(io.BytesIO(archive)) as files:
                entry = files.getinfo("coverage-summary.json")
                if entry.file_size > 10_000_000:
                    raise ValueError("Coverage baseline exceeds the 10 MB size limit.")
                payload = files.read(entry)
            identity = json.loads(payload)
            if identity["commit"] != run["head_sha"] or identity.get("workingTreeDirty", True):
                raise ValueError("Coverage baseline does not identify its successful push build.")
            return payload
    return None


def from_release(repo: str) -> bytes | None:
    # Include prereleases: alpha/beta history does not appear in /releases/latest.
    for release in read_json(f"repos/{repo}/releases?per_page=100"):
        if release["draft"]:
            continue
        for asset in release["assets"]:
            if asset["name"] == "coverage-summary.json":
                if asset["size"] > 10_000_000:
                    raise ValueError("Coverage baseline exceeds the 10 MB size limit.")
                return github(f"repos/{repo}/releases/assets/{asset['id']}", binary=True)
    return None


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", choices=("ci", "release"), required=True)
    parser.add_argument("--branch")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    repo = os.environ["GH_REPO"]
    if args.output.exists():
        raise ValueError("Baseline destination already exists; use a fresh path.")
    if args.source == "ci" and not args.branch:
        parser.error("--branch is required for a CI baseline")
    try:
        payload = from_ci(repo, args.branch) if args.source == "ci" else from_release(repo)
        if payload is None:
            print("No retained coverage baseline found. This build establishes one.")
            return
        json.loads(payload)
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_bytes(payload)
        print("Downloaded the previous coverage baseline for advisory comparison.")
    except (subprocess.SubprocessError, ValueError, KeyError, zipfile.BadZipFile) as error:
        # A first build, restricted fork token or expired artefact must not block
        # tests. Keep the failure visible without printing credential-bearing output.
        print(f"::warning::Coverage baseline unavailable ({type(error).__name__}); no comparison will be made.")


if __name__ == "__main__":
    main()
