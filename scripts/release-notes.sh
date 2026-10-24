#!/usr/bin/env bash
# Release notes for a tag, from its Conventional Commits since the previous
# tag: breaking changes, features, fixes and other changes to the code, one
# line each with its commit. Docs, tests, CI and chores are left out.
#
#   scripts/release-notes.sh v0.2.0
#
# Needs the history and tags (a full clone: actions/checkout fetch-depth 0).
set -euo pipefail

tag=${1:?tag}
previous=$(git describe --tags --abbrev=0 --match 'v*' "$tag^" 2>/dev/null || true)
range=${previous:+$previous..}$tag

# "feat(cli)!: text" -> "**cli:** text", with the short hash after it.
subjects() {
    git log --no-merges --reverse --format='%s (%h)' "$range" |
        grep -E "^($1)(\([^)]*\))?$2: " |
        sed -E 's/^[a-z]+\(([^)]*)\)!?: /**\1:** /; s/^[a-z]+!?: //; s/^/- /' || true
}

section() {
    local lines
    lines=$(subjects "$2" "$3")
    if [ -n "$lines" ]; then printf '### %s\n\n%s\n\n' "$1" "$lines"; fi
}

section 'Breaking changes' '[a-z]+' '!'
section 'Features' 'feat' ''
section 'Fixes' 'fix' ''
section 'Other changes' 'perf|refactor|revert' ''

if [ -n "$previous" ]; then
    echo "Changes since $previous: \`git log $previous..$tag\`."
fi
