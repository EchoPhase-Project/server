# Shared helpers for the tasks in mise-tasks/ (sourced, not run). Expects the
# [env] of mise.toml and the repository root as the working directory.

# Image tag of this checkout, unless IMAGE_TAG is set (see scripts/image-tag.sh).
image_tag() {
    echo "${IMAGE_TAG:-$(scripts/image-tag.sh)}"
}
