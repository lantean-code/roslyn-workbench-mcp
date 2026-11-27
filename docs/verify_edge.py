#!/usr/bin/env python3
"""Verify the public Worker contract after a separately authorised cutover."""
from __future__ import annotations

import hashlib
from html.parser import HTMLParser
import json
from pathlib import Path
import time
import urllib.error
import urllib.request
from urllib.parse import urljoin, urlparse


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


class PageIdentity(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.capture = None
        self.title = []
        self.headings = []
        self.canonical = None

    def handle_starttag(self, tag, attrs):
        values = dict(attrs)
        if tag in {"title", "h1"}:
            self.capture = tag
        if tag == "link" and values.get("rel") == "canonical":
            self.canonical = values.get("href")

    def handle_endtag(self, tag):
        if tag == self.capture:
            self.capture = None

    def handle_data(self, data):
        if self.capture == "title":
            self.title.append(data)
        if self.capture == "h1":
            self.headings.append(data)

    def identity(self):
        return (" ".join("".join(self.title).split()), " ".join("".join(self.headings).split()))


def request(url):
    req = urllib.request.Request(url, headers={"User-Agent": "Lantean-Pages-Edge-Verification", "Cache-Control": "no-cache"})
    opener = urllib.request.build_opener(NoRedirect())
    try:
        response = opener.open(req, timeout=20)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        return response.code, response.headers, response.read(16 * 1024 * 1024 + 1)


def check_edge_site(site: Path, base_url: str, read=request):
    base = base_url.rstrip("/") + "/"
    if urlparse(base).path != "/" or urlparse(base).scheme not in {"http", "https"}:
        raise ValueError("Verify the product-site root, not a repository or docs subpath.")
    inventory = json.loads((site / "docs/versions.json").read_text(encoding="utf-8"))
    latest = next((entry["version"] for entry in inventory if "latest" in entry["aliases"]), None)

    def get(path):
        # This cache-busting query is also deliberately preserved by redirects.
        return read(urljoin(base, path) + "?pages-verification=1")

    def page(path):
        status, _, content = get(path)
        if status != 200:
            raise ValueError(f"Expected HTTP 200 for {path}, got {status}.")
        expected, actual = PageIdentity(), PageIdentity()
        expected.feed((site / path).read_text(encoding="utf-8"))
        actual.feed(content.decode("utf-8"))
        if not expected.identity()[0] or expected.identity() != actual.identity():
            raise ValueError(f"Unexpected page identity for {path}.")
        if actual.canonical and not actual.canonical.startswith(base):
            raise ValueError(f"Unexpected canonical domain for {path}.")

    def redirect(path, target, expected_status):
        status, headers, _ = get(path)
        destination = urljoin(base, target) + "?pages-verification=1"
        if status != expected_status or headers.get("Location") != destination:
            raise ValueError(f"Unexpected redirect for {path}: HTTP {status}, {headers.get('Location')}.")
        if expected_status == 302 and headers.get("Cache-Control") != "no-store":
            raise ValueError(f"Moving alias is browser-cacheable: {path}.")

    status, _, content = get("docs/versions.json")
    if status != 200 or json.loads(content) != inventory:
        raise ValueError("The public version manifest does not match this publication.")
    page("index.html")
    if latest:
        redirect("docs/", f"docs/{latest}/", 302)
    for entry in inventory:
        version = entry["version"]
        page(f"docs/{version}/index.html")
        redirect(f"{version}/index.html", f"docs/{version}/index.html", 301)
        for alias in entry["aliases"]:
            redirect(f"docs/{alias}/index.html", f"docs/{version}/index.html", 302)
            redirect(f"{alias}/index.html", f"docs/{version}/index.html", 302)
        catalog = "reference/tools/catalog.json"
        local_catalog = site / "docs" / version / catalog
        if local_catalog.is_file():
            status, _, body = get(f"docs/{version}/{catalog}")
            actual, expected = json.loads(body), json.loads(local_catalog.read_bytes())
            if status != 200 or any(actual.get(key) != expected.get(key) for key in ("sourceTag", "productVersion", "commit")):
                raise ValueError(f"Published catalogue has the wrong release identity: {version}.")
        for local in (site / "docs" / version / "agent").rglob("*.zip"):
            suffix = local.relative_to(site / "docs" / version).as_posix()
            status, _, body = get(f"docs/{version}/{suffix}")
            if status != 200 or hashlib.sha256(body).digest() != hashlib.sha256(local.read_bytes()).digest():
                raise ValueError(f"Download differs from the publication: {version}/{suffix}.")


def verify_edge_site(site: Path, base_url: str):
    deadline = time.monotonic() + 300
    last_error = None
    for _ in range(30):
        try:
            check_edge_site(site, base_url)
            print("Verified public documentation HTTP redirects, manifest and page identities.")
            return
        except (ValueError, urllib.error.URLError, TimeoutError) as error:
            last_error = error
        if time.monotonic() >= deadline:
            break
        time.sleep(10)
    raise ValueError(f"Public edge verification failed: {last_error}")
