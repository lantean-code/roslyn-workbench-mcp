#!/usr/bin/env python3
"""Deploy one Pages artifact using its generated-site commit and verify its public content."""

from __future__ import annotations

import argparse
from contextlib import contextmanager
import json
import os
from pathlib import Path
import re
import signal
import time
import urllib.error
import urllib.request


def request_bytes(url: str, token: str | None = None, payload: dict | None = None, *, timeout: float = 30) -> bytes:
    headers = {"User-Agent": "Roslyn-Workbench-Pages-publisher"}
    data = None
    if token is not None:
        headers["Authorization"] = f"Bearer {token}"
    if payload is not None:
        headers["Content-Type"] = "application/json"
        data = json.dumps(payload).encode("utf-8")

    request = urllib.request.Request(url, data=data, headers=headers)
    with urllib.request.urlopen(request, timeout=timeout) as response:
        return response.read()


@contextmanager
def cancellation_signals():
    def stop(signum, _frame):
        raise SystemExit(128 + signum)

    previous = {signum: signal.getsignal(signum) for signum in (signal.SIGINT, signal.SIGTERM)}
    try:
        for signum in previous:
            signal.signal(signum, stop)
        yield
    finally:
        for signum, handler in previous.items():
            signal.signal(signum, handler)


def cancel_pending_deployment(status_url: str, token: str) -> None:
    try:
        request_bytes(f"{status_url}/cancel", token, {}, timeout=5)
    except Exception:
        print("::warning::Could not confirm cancellation of the pending Pages deployment; inspect it before retrying.")


def poll_status(status_url: str, token: str) -> str:
    failures = 0
    deadline = time.monotonic() + 600
    for _ in range(60):
        if time.monotonic() >= deadline:
            break

        try:
            status = json.loads(request_bytes(status_url, token))["status"]
        except (urllib.error.URLError, TimeoutError) as error:
            if isinstance(error, urllib.error.HTTPError) and error.code != 429 and error.code < 500:
                raise

            failures += 1
            if failures >= 5:
                raise RuntimeError("Pages status polling failed five consecutive times.") from None

            print(f"Pages status temporarily unavailable; retrying ({failures}/5).")
        else:
            failures = 0
            if status in {"succeed", "deployment_failed", "deployment_content_failed", "deployment_cancelled", "deployment_lost"}:
                return status

        time.sleep(10)

    raise TimeoutError("Pages deployment did not finish within ten minutes.")


def deploy(artifact_id: int, environment: dict[str, str]) -> None:
    build_version = environment.get("PAGES_BUILD_VERSION", "")
    if re.fullmatch(r"[0-9a-f]{40}", build_version) is None:
        raise ValueError("PAGES_BUILD_VERSION must be the full generated-site commit SHA.")

    oidc_response = request_bytes(
        environment["ACTIONS_ID_TOKEN_REQUEST_URL"],
        environment["ACTIONS_ID_TOKEN_REQUEST_TOKEN"],
    )
    oidc_token = json.loads(oidc_response)["value"]
    print(f"::add-mask::{oidc_token}", flush=True)

    endpoint = f"{environment['GITHUB_API_URL']}/repos/{environment['GITHUB_REPOSITORY']}/pages/deployments"
    token = environment["GH_TOKEN"]
    payload = {"artifact_id": artifact_id, "pages_build_version": build_version, "oidc_token": oidc_token}
    with cancellation_signals():
        pending = True
        status_url = f"{endpoint}/{build_version}"
        try:
            deployment = json.loads(request_bytes(endpoint, token, payload))
            deployment_id = deployment.get("id") or build_version
            status_url = f"{endpoint}/{deployment_id}"
            print(f"Created Pages deployment {deployment_id} for artifact {artifact_id}.")

            status = poll_status(status_url, token)
            pending = False
            if status != "succeed":
                raise ValueError(f"Pages deployment failed: {status}.")
        finally:
            if pending:
                cancel_pending_deployment(status_url, token)


def public_checks(site: Path) -> list[Path]:
    inventory = json.loads((site / "versions.json").read_text(encoding="utf-8"))
    paths = [Path("versions.json"), Path("index.html")]
    for entry in inventory:
        version = entry["version"]
        if not isinstance(version, str) or not version or version in {".", ".."} or "/" in version or "\\" in version:
            raise ValueError("Invalid version in the Pages inventory.")

        paths.append(Path(version) / "index.html")
        for alias in entry["aliases"]:
            if alias != "latest":
                raise ValueError("Invalid alias in the Pages inventory.")
            paths.append(Path(alias) / "index.html")

        for name in ("compatibility.html", "reference/tools/catalog.json"):
            path = Path(version) / name
            if (site / path).is_file():
                paths.append(path)
    return paths


def verify_public_site(site: Path, base_url: str) -> None:
    expected = {path.as_posix(): (site / path).read_bytes() for path in public_checks(site)}
    pending = dict(expected)
    deadline = time.monotonic() + 300
    for attempt in range(30):
        for path, content in list(pending.items()):
            if time.monotonic() >= deadline:
                raise TimeoutError("Public Pages verification exceeded five minutes.")
            url = f"{base_url.rstrip('/')}/{path}?pages-verification={attempt}-{time.time_ns()}"
            try:
                actual = request_bytes(url)
            except (urllib.error.URLError, TimeoutError):
                continue

            if actual == content:
                del pending[path]

        if not pending:
            print(f"Verified {len(expected)} public Pages resources against the uploaded site.")
            return
        time.sleep(10)

    raise ValueError(f"Public Pages content does not match the uploaded site: {', '.join(sorted(pending))}.")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--artifact-id", type=int, required=True)
    parser.add_argument("--site", type=Path, required=True)
    parser.add_argument("--url", required=True)
    arguments = parser.parse_args()
    deploy(arguments.artifact_id, dict(os.environ))
    verify_public_site(arguments.site, arguments.url)
    with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8") as output:
        output.write(f"page_url={arguments.url.rstrip('/')}/\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
