#!/usr/bin/env python3
"""Delete old image tags from a registry (distribution API, delete enabled).

Kept per repository:
  - the newest KEEP build tags (by the image's creation time),
  - "latest", release tags (1.2.3) and BuildKit cache tags (*buildcache),
  - every tag a revision of the Helm release still references, so
    `mise run helm:rollback` always has its images,
  - anything sharing a digest with a kept tag: the API deletes manifests, not
    tags, and every tag on that manifest would go with it.

Only manifests are deleted; `registry garbage-collect` frees the blobs after.

usage: registry-prune.py REGISTRY KEEP DRY_RUN(true|false) REPO...
(run through `mise run registry:prune`)
"""
import json
import re
import subprocess
import sys
import urllib.request

ACCEPT = ", ".join([
    "application/vnd.oci.image.index.v1+json",
    "application/vnd.oci.image.manifest.v1+json",
    "application/vnd.docker.distribution.manifest.list.v2+json",
    "application/vnd.docker.distribution.manifest.v2+json",
])
RELEASE = re.compile(r"^\d+\.\d+\.\d+$")


def request(url, method="GET", accept=ACCEPT):
    req = urllib.request.Request(url, method=method, headers={"Accept": accept})
    return urllib.request.urlopen(req, timeout=30)


def get_json(url, accept=ACCEPT):
    with request(url, accept=accept) as r:
        return json.load(r), r.headers.get("Docker-Content-Digest")


def created(base, repo, tag):
    """Digest of the tag and the creation time of its image config."""
    manifest, digest = get_json(f"{base}/v2/{repo}/manifests/{tag}")
    if "manifests" in manifest:  # an index: date it by its first image
        child = next(m for m in manifest["manifests"]
                     if m.get("platform", {}).get("os") != "unknown")
        manifest, _ = get_json(f"{base}/v2/{repo}/manifests/{child['digest']}")
    config, _ = get_json(f"{base}/v2/{repo}/blobs/{manifest['config']['digest']}", accept="*/*")
    # Images carry it in their config; oras artifacts (empty config) in an annotation.
    return digest, config.get("created") or manifest.get("annotations", {}).get("org.opencontainers.image.created", "")


def deployed_tags():
    """Image tags in every revision of the Helm release (best effort)."""
    try:
        history = json.loads(subprocess.run(
            ["helm", "history", "echophase", "-n", "echophase", "-o", "json"],
            check=True, capture_output=True, text=True).stdout)
    except (OSError, subprocess.CalledProcessError, json.JSONDecodeError):
        print("warning: no Helm history, keeping by age only", file=sys.stderr)
        return set()
    tags = set()
    for rev in history:
        values = json.loads(subprocess.run(
            ["helm", "get", "values", "echophase", "-n", "echophase", "-a",
             "--revision", str(rev["revision"]), "-o", "json"],
            check=True, capture_output=True, text=True).stdout)
        for key in ("echophase", "webapp", "migrate"):
            tag = values.get(key, {}).get("image", {}).get("tag")
            if tag:
                tags.add(str(tag))
    return tags


def main():
    registry, keep, dry_run, repos = sys.argv[1], int(sys.argv[2]), sys.argv[3] == "true", sys.argv[4:]
    base = f"http://{registry}"
    pinned = deployed_tags()

    for repo in repos:
        tags = get_json(f"{base}/v2/{repo}/tags/list")[0].get("tags") or []
        info = {}
        for t in tags:
            try:
                info[t] = created(base, repo, t)
            except (KeyError, StopIteration, urllib.error.HTTPError):
                # Not an image (e.g. a cache manifest): undated, never a build.
                info[t] = (get_json(f"{base}/v2/{repo}/manifests/{t}")[1], "")
        special = {t for t in tags if t == "latest" or RELEASE.match(t) or t.endswith("buildcache")}
        builds = sorted((t for t in tags if t not in special),
                        key=lambda t: info[t][1], reverse=True)
        kept = set(builds[:keep]) | special | (pinned & set(tags))
        kept_digests = {info[t][0] for t in kept}
        doomed = {}
        for t in tags:
            if t not in kept and info[t][0] not in kept_digests:
                doomed.setdefault(info[t][0], []).append(t)

        print(f"{repo}: {len(tags)} tags, keeping {len(tags) - sum(map(len, doomed.values()))}, "
              f"deleting {sum(map(len, doomed.values()))}")
        for digest, ts in sorted(doomed.items(), key=lambda kv: info[kv[1][0]][1]):
            print(f"  {'would delete' if dry_run else 'delete'} {', '.join(sorted(ts))} ({digest[:19]})")
            if not dry_run:
                request(f"{base}/v2/{repo}/manifests/{digest}", method="DELETE").close()


if __name__ == "__main__":
    main()
