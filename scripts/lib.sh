# Shared helpers for the tasks in mise-tasks/ (sourced, not run). Expects the
# [env] of mise.toml and the repository root as the working directory.

# Image tag of this checkout, unless IMAGE_TAG is set (see scripts/image-tag.sh).
image_tag() {
    echo "${IMAGE_TAG:-$(scripts/image-tag.sh)}"
}

# "<image> <build context>" for "server", "webapp" or "all".
component_images() {
    case "$1" in
        server) echo "$SERVER_IMAGE src" ;;
        webapp) echo "$WEBAPP_IMAGE webapp" ;;
        all) component_images server; component_images webapp ;;
        *) echo "unknown component '$1'" >&2; return 1 ;;
    esac
}

# Fills HELM_ARGS: namespace, the values files that exist, and the registry
# and image tag of this checkout, so every install or upgrade deploys exactly
# the images docker:publish pushed for it. With "reuse" (CI, which has no
# helm/secrets.yaml) the release's previous values replace the files.
helm_args() {
    local tag
    tag=$(image_tag)
    HELM_ARGS=(--namespace "$HELM_NAMESPACE")
    if [ "${1:-}" = reuse ]; then
        HELM_ARGS+=(--reset-then-reuse-values)
    else
        for f in "$HELM_VALUES" "$HELM_SECRETS"; do
            if [ -f "helm/$f" ]; then HELM_ARGS+=(--values "helm/$f"); fi
        done
    fi
    HELM_ARGS+=(--set "imageRegistry=$DOCKER_REGISTRY,echophase.image.tag=$tag,migrate.image.tag=$tag,webapp.image.tag=$tag")
}

# Fails, before Helm waits on pods that cannot pull, unless both images of this
# checkout's tag are in the registry.
check_images() {
    local tag image
    tag=$(image_tag)
    for image in "$SERVER_IMAGE" "$WEBAPP_IMAGE"; do
        if ! curl -fsSI -o /dev/null \
                -H 'Accept: application/vnd.oci.image.index.v1+json, application/vnd.oci.image.manifest.v1+json, application/vnd.docker.distribution.manifest.list.v2+json, application/vnd.docker.distribution.manifest.v2+json' \
                "http://$REGISTRY_API/v2/$image/manifests/$tag"; then
            echo "$DOCKER_REGISTRY/$image:$tag is not in the registry: run 'mise run docker:publish' first" >&2
            return 1
        fi
    done
}
