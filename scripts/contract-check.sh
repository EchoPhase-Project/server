#!/usr/bin/env bash
# Fails when an API contract breaks clients of its compatibility series.
#
#   scripts/contract-check.sh <repository> <openapi.json> [oras options...]
#   e.g. scripts/contract-check.sh registry.registry.svc:5000/echophase/openapi artifacts/openapi.json --plain-http
#
# A series is what semver promises compatibility within: the major version
# (1.x.y), or for 0.x the minor one (0.2.y: 0.x may break with every minor).
# The contract is compared with the newest release (X.Y.Z) published in its
# own series; a breaking change there fails. In a new series there is no such
# release: breaking changes are allowed, and listed against the previous one.
#
# This build's version: CONTRACT_VERSION (X.Y.Z, e.g. from a release tag),
# else the checkout's release tag, else the next one as `cog bump --auto`
# (cocogitto 7) will make it from the commits since that tag:
#   from 1.0: a breaking commit ("type!:", "BREAKING CHANGE:") -> next major
#   on 0.x:   a feat (feat!: too) -> next minor; a breaking fix!, refactor!
#             and the like stay in the series (cog does not bump for them)
#
# Breaking changes accepted within a series anyway are listed in
# openapi.err-ignore (CONTRACT_ERR_IGNORE overrides the path); see that file.
set -euo pipefail

repository=${1:?repository}
contract=${2:?openapi.json}
shift 2
oras_options=("$@")

semver='^[0-9]+\.[0-9]+\.[0-9]+$'


# The series a version belongs to: "2" for 2.3.1, "0.4" for 0.4.1.
series_of() {
    local major minor
    IFS=. read -r major minor _ <<<"$1"
    if [ "$major" -gt 0 ]; then echo "$major"; else echo "0.$minor"; fi
}

# The series after it: what a breaking change moves to.
next_series() {
    local major minor
    IFS=. read -r major minor _ <<<"$1"
    if [ "$major" -gt 0 ]; then echo "$((major + 1))"; else echo "0.$((minor + 1))"; fi
}

version_series() {
    if [ -n "${CONTRACT_VERSION:-}" ]; then
        [[ $CONTRACT_VERSION =~ $semver ]] || { echo "CONTRACT_VERSION '$CONTRACT_VERSION' is not X.Y.Z" >&2; exit 2; }
        series_of "$CONTRACT_VERSION"
        return
    fi

    local tag
    if ! tag=$(git describe --tags --abbrev=0 --match 'v[0-9]*' 2>/dev/null); then
        echo "0.0"   # no release yet: nothing to be compatible with
        return
    fi
    local released=${tag#v}
    if [ "$(git rev-list -n1 "$tag")" = "$(git rev-parse HEAD)" ]; then
        series_of "$released"
    elif [ "${released%%.*}" -gt 0 ] && breaking_since "$tag"; then
        next_series "$released"
    elif [ "${released%%.*}" -eq 0 ] && git log --format='%s' "$tag..HEAD" | grep -qE '^feat(\([^)]*\))?!?:'; then
        next_series "$released"
    else
        series_of "$released"
    fi
}

breaking_since() {
    git log --format='%s' "$1..HEAD" | grep -qE '^[A-Za-z]+(\([^)]*\))?!:' \
        || git log --format='%b' "$1..HEAD" | grep -qE '^BREAKING[ -]CHANGE:'
}

series=$(version_series)

# Releases published so far, oldest first (main builds' tags like
# 0.2.0-3-gabc and "latest" are not releases). A repository that does not
# exist yet has none; any other registry error fails the check rather than
# letting it pass unchecked.
if ! tags=$(oras repo tags "${oras_options[@]}" "$repository" 2>&1); then
    if grep -qiE 'not found|name unknown|NAME_UNKNOWN' <<<"$tags"; then
        tags=""
    else
        echo "cannot list $repository: $tags" >&2
        exit 2
    fi
fi
mapfile -t releases < <(grep -E "$semver" <<<"$tags" | sort -V || true)

base="" previous=""
for release in "${releases[@]}"; do
    if [ "$(series_of "$release")" = "$series" ]; then
        base=$release
    elif [ -z "$base" ]; then
        previous=$release   # the newest release before this series
    fi
done

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

# oasdiff has no comments: it ignores any change a line's text matches, "#"
# lines included. Hand it only the entries.
err_ignore=${CONTRACT_ERR_IGNORE:-"$(dirname "$0")/../openapi.err-ignore"}
ignore_options=()
accepted=0
if [ -f "$err_ignore" ]; then
    grep -vE '^[[:space:]]*(#|$)' "$err_ignore" > "$work/err-ignore" || true
    accepted=$(wc -l < "$work/err-ignore")
    [ "$accepted" -gt 0 ] && ignore_options=(--err-ignore "$work/err-ignore")
fi

pull() {
    oras pull "${oras_options[@]}" "$repository:$1" -o "$work/$1" >/dev/null
    echo "$work/$1/openapi.json"
}

if [ -n "$base" ]; then
    echo "series $series: comparing with $base"
    [ "$accepted" -gt 0 ] && echo "accepted as breaking anyway (not checked): $accepted in $err_ignore"
    if ! oasdiff breaking "$(pull "$base")" "$contract" --fail-on ERR "${ignore_options[@]}"; then
        echo "breaking changes within series $series." >&2
        if [ "${series%%.*}" = 0 ]; then
            echo "Before 1.0 they need the next minor: a feat!: commit, or cog bump --minor." >&2
        else
            echo "They need the next major: mark a commit breaking (type!: or BREAKING CHANGE:)." >&2
        fi
        echo "Or accept one in this series: add its line (METHOD path message) to $err_ignore." >&2
        exit 1
    fi
    echo "no breaking changes against $base"
elif [ -n "$previous" ]; then
    echo "series $series is new: breaking changes are allowed; against $previous they are:"
    oasdiff breaking "$(pull "$previous")" "$contract"
else
    echo "series $series: no published release to compare with"
fi
