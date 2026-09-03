#!/usr/bin/env python3
"""Shared inspection helpers for Linux package validation scripts."""

import hashlib
import json
import os
import queue
import shutil
import subprocess
import tempfile
import threading
import time
from pathlib import Path


PACKAGE = "roslyn-workbench-mcp"
APPLICATION = Path("usr/lib") / PACKAGE
COMMAND = Path("usr/bin") / PACKAGE


def capture(command: list[str]) -> str:
    """Run a command and return its trimmed standard output."""
    return subprocess.check_output(command, text=True).strip()


def digest(path: Path) -> str:
    """Return the hexadecimal SHA-256 digest of a file."""
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def smoke(executable: Path, version: str, logs: Path, phase: str, client_name: str) -> None:
    """Exercise redirected MCP stdio as an unprivileged user."""
    state = Path(tempfile.mkdtemp(prefix="rwmcp-package-state-", dir="/tmp"))
    environment = os.environ.copy()
    environment["HOME"] = str(state)
    environment["XDG_DATA_HOME"] = str(state)
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
                    "clientInfo": {"name": client_name, "version": "1.0"},
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
                    process.terminate()
                    try:
                        process.wait(timeout=5)
                    except subprocess.TimeoutExpired:
                        process.kill()
                        process.wait()
                process.stdout.close()
    finally:
        shutil.rmtree(state)
