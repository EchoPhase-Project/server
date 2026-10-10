#!/usr/bin/env bash
# Prints the image tag for the current checkout (Docker tags allow [A-Za-z0-9_.-]):
#
#   on tag v1.2.0, clean          1.2.0
#   3 commits after v1.2.0        1.2.0-3-g938156d
#   no release tag yet            0.0.0-g938156d
#   uncommitted changes           <one of the above>-dirty.20261010T153012
#
# The dirty suffix is the newest modification time among the changed files,
# not the current time: the same working tree gives the same tag in every
# task run (docker:publish, then helm:upgrade), and any edit gives a new one.
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

if described=$(git describe --tags --match 'v[0-9]*' 2>/dev/null); then
    tag=${described#v}
else
    tag="0.0.0-g$(git rev-parse --short HEAD)"
fi

if [ -n "$(git status --porcelain)" ]; then
    newest=$(git status --porcelain -z --untracked-files=all \
        | tr '\0' '\n' \
        | sed -E 's/^.. //' \
        | while IFS= read -r f; do [ -f "$f" ] && stat -c %Y -- "$f"; done \
        | sort -n | tail -1)
    # Only deletions: no file left to date, fall back to now.
    tag="$tag-dirty.$(date -u -d "@${newest:-$(date +%s)}" +%Y%m%dT%H%M%S)"
fi

echo "$tag"
