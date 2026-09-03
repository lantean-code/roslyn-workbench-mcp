#!/usr/bin/env python3
"""Inspect and smoke-test a built DEB; opt into installation only on a disposable Linux system."""

import argparse
import hashlib
import json
import os
import platform
import queue
import shutil
import subprocess
import tarfile
import tempfile
import threading
import time
from pathlib import Path


_PACKAGE = "roslyn-workbench-mcp"
_APPLICATION = Path("usr/lib") / _PACKAGE
_COMMAND = Path("usr/bin") / _PACKAGE


def capture(command: list[str]) -> str:
    return subprocess.check_output(command, text=True).strip()


def digest(path: Path) -> str:
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def smoke(executable: Path, version: str, logs: Path, phase: str = "extracted") -> None:
    """Exercise real redirected stdio as an unprivileged user, including when installed by root."""
    state = Path(tempfile.mkdtemp(prefix="rwmcp-deb-state-", dir="/tmp"))
    environment = os.environ.copy()
    environment["HOME"] = str(state)
    environment["XDG_DATA_HOME"] = str(state)
    # Root must not run the server with administrator authority during an install check.
    identity = {}
    if os.geteuid() == 0:
        os.chown(state, 65534, 65534)
        identity = {"user": 65534, "group": 65534, "extra_groups": []}
    try:
        actual = subprocess.check_output([str(executable), "--version"], text=True, env=environment, cwd=state, timeout=30, **identity).strip()
        if actual != version:
            raise ValueError(f"Host version {actual!r} does not match {version!r}.")
        with (logs / f"{phase}-host-stderr.log").open("w") as stderr:
            process = subprocess.Popen([
                str(executable), "--state-directory", str(state), "--error-reporting-consent", "never",
            ], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=stderr, text=True, env=environment, cwd=state, **identity)
            messages = queue.Queue()

            def read_messages() -> None:
                for line in process.stdout:
                    messages.put(line)
                messages.put(None)

            threading.Thread(target=read_messages, daemon=True).start()

            def send(value: dict) -> None:
                process.stdin.write(json.dumps(value) + "\n")
                process.stdin.flush()

            def response(identifier: int) -> dict:
                deadline = time.monotonic() + 30
                while True:
                    line = messages.get(timeout=max(0.001, deadline - time.monotonic()))
                    if line is None:
                        raise ValueError("Host exited before returning its MCP response.")
                    value = json.loads(line)
                    if value.get("id") == identifier:
                        if "error" in value:
                            raise ValueError(f"MCP request failed: {value['error']}")
                        return value["result"]
                    if time.monotonic() >= deadline:
                        raise TimeoutError("Timed out waiting for MCP response.")

            try:
                send({"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {
                    "protocolVersion": "2025-06-18", "capabilities": {},
                    "clientInfo": {"name": "deb-packaging-check", "version": "1.0"},
                }})
                initialized = response(1)
                send({"jsonrpc": "2.0", "method": "notifications/initialized"})
                send({"jsonrpc": "2.0", "id": 2, "method": "tools/list", "params": {}})
                tools = response(2)
                names = {tool["name"] for tool in tools["tools"]}
                if not {"server-status", "workspace-open"}.issubset(names):
                    raise ValueError("Installed Host did not publish the expected lifecycle tools.")
                (logs / f"{phase}-mcp-smoke.json").write_text(json.dumps({"initialize": initialized, "toolNames": sorted(names)}, indent=2) + "\n")
            finally:
                process.stdin.close()
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    # Match the existing acceptance client's bounded EOF cleanup.
                    process.terminate()
                    try:
                        process.wait(timeout=5)
                    except subprocess.TimeoutExpired:
                        process.kill()
                        process.wait()
                process.stdout.close()
    finally:
        shutil.rmtree(state)


def inspect(package: Path, version: str, root: Path, publish: Path | None) -> None:
    expected = {
        "Package": _PACKAGE, "Version": version.replace("-", "~", 1) + "-1",
        "Architecture": "amd64", "Depends": "dotnet-runtime-10.0 (>= 10.0.0)",
    }
    for field, value in expected.items():
        if capture(["dpkg-deb", "--field", str(package), field]) != value:
            raise ValueError(f"Incorrect package {field}.")
    expected_checksum = f"{digest(package)}  {package.name}\n"
    if package.with_suffix(".deb.sha256").read_text(encoding="ascii") != expected_checksum:
        raise ValueError("Package checksum does not match its adjacent checksum file.")
    archive_path = root.parent / "payload.tar"
    with archive_path.open("wb") as stream:
        subprocess.run(["dpkg-deb", "--fsys-tarfile", str(package)], stdout=stream, check=True)
    with tarfile.open(archive_path) as archive:
        for member in archive:
            if member.uid != 0 or member.gid != 0:
                raise ValueError(f"Package entry is not root-owned: {member.name}")
            path = Path(member.name)
            if member.isfile() and path != _APPLICATION / _PACKAGE and member.mode != 0o644:
                raise ValueError(f"Unexpected payload permissions: {member.name}")
            if member.isdir() and member.mode != 0o755:
                raise ValueError(f"Unexpected directory permissions: {member.name}")
    subprocess.run(["dpkg-deb", "--raw-extract", str(package), str(root)], check=True)
    if {path.name for path in (root / "DEBIAN").iterdir()} != {"control", "md5sums"}:
        raise ValueError("The package must not contain maintainer scripts or configuration hooks.")
    subprocess.run(["md5sum", "--check", "--status", "DEBIAN/md5sums"], cwd=root, check=True)
    executable = root / _APPLICATION / _PACKAGE
    if executable.stat().st_mode & 0o777 != 0o755:
        raise ValueError("Host executable permissions must be 0755.")
    if os.readlink(root / _COMMAND) != "../lib/roslyn-workbench-mcp/roslyn-workbench-mcp":
        raise ValueError("Incorrect command symlink.")
    for name in ("Roslyn.Workbench.Mcp.dll", "Roslyn.Workbench.Mcp.pdb", "Roslyn.Workbench.Mcp.runtimeconfig.json", "Roslyn.Workbench.Mcp.Plugins.Core.dll", "LICENSE", "THIRD-PARTY-NOTICES.md"):
        if not (root / _APPLICATION / name).is_file():
            raise ValueError(f"Missing published Host file: {name}")
    if publish:
        originals = {path.relative_to(publish) for path in publish.rglob("*") if path.is_file()}
        packaged = {path.relative_to(root / _APPLICATION) for path in (root / _APPLICATION).rglob("*") if path.is_file()}
        if originals != packaged:
            raise ValueError("Packaged file inventory differs from the published Host.")
        for relative in originals:
            if digest(publish / relative) != digest(root / _APPLICATION / relative):
                raise ValueError(f"Packaged Host file differs: {relative}")


def installed_check(package: Path, version: str, logs: Path, previous: Path | None) -> None:
    if os.geteuid() != 0:
        raise ValueError("--install requires root on a disposable Linux system.")
    status = subprocess.run(["dpkg-query", "--show", "--showformat=${db:Status-Abbrev}", _PACKAGE], capture_output=True, text=True)
    if status.returncode == 0 or os.path.lexists("/" + str(_COMMAND)) or os.path.lexists("/" + str(_APPLICATION)):
        raise ValueError("Refusing to modify an existing package, command or application directory.")
    if previous:
        if capture(["dpkg-deb", "--field", str(previous), "Package"]) != _PACKAGE:
            raise ValueError("Upgrade baseline must be a Roslyn Workbench package.")
        subprocess.run(["dpkg", "--compare-versions", capture(["dpkg-deb", "--field", str(previous), "Version"]), "lt", capture(["dpkg-deb", "--field", str(package), "Version"])], check=True)
    with (logs / "apt.log").open("w") as output:
        def apt(*arguments: str) -> None:
            subprocess.run(["apt-get", "--yes", "--no-install-recommends", *arguments], stdout=output, stderr=subprocess.STDOUT, check=True)

        try:
            if previous:
                apt("install", str(previous))
            apt("install", str(package))
            verification = capture(["dpkg", "--verify", _PACKAGE])
            if verification:
                raise ValueError(f"Installed files differ from the package inventory:\n{verification}")
            smoke(Path("/") / _COMMAND, version, logs, "installed")
            apt("install", "--reinstall", str(package))
            verification = capture(["dpkg", "--verify", _PACKAGE])
            if verification:
                raise ValueError(f"Reinstalled files differ from the package inventory:\n{verification}")
            smoke(Path("/") / _COMMAND, version, logs, "reinstalled")
        finally:
            status = subprocess.run(["dpkg-query", "--show", "--showformat=${db:Status-Abbrev}", _PACKAGE], capture_output=True, text=True)
            if status.returncode == 0:
                apt("remove", _PACKAGE)
    if os.path.lexists("/" + str(_COMMAND)) or os.path.exists("/" + str(_APPLICATION)):
        raise ValueError("Removal left installer-owned paths behind.")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", required=True, type=Path)
    parser.add_argument("--expected-version", required=True)
    parser.add_argument("--publish-directory", type=Path)
    parser.add_argument("--install", action="store_true")
    parser.add_argument("--upgrade-from", type=Path)
    args = parser.parse_args()
    if platform.system() != "Linux" or platform.machine() != "x86_64":
        raise ValueError("Run package validation on Linux x64.")
    if args.upgrade_from and not args.install:
        raise ValueError("--upgrade-from requires --install.")
    package = args.package.resolve(strict=True)
    logs = Path(tempfile.mkdtemp(prefix="validation-", dir=package.parent))
    with tempfile.TemporaryDirectory(prefix="rwmcp-deb-check-", dir="/tmp") as temporary:
        # Permit the unprivileged smoke-test child to traverse the extracted files.
        Path(temporary).chmod(0o755)
        root = Path(temporary) / "root"
        inspect(package, args.expected_version, root, args.publish_directory)
        if args.install:
            previous = args.upgrade_from.resolve(strict=True) if args.upgrade_from else None
            installed_check(package, args.expected_version, logs, previous)
        else:
            smoke(root / _COMMAND, args.expected_version, logs)
    print(f"PASS: DEB metadata, checksum, permissions, payload and MCP launch. Logs: {logs}")
    if args.install:
        print("PASS: APT installation, reinstallation and removal" + (", including upgrade." if args.upgrade_from else "."))


if __name__ == "__main__":
    main()
