# CI image for Woodpecker steps: mise with every tool of mise.lock installed,
# so steps run `mise run <task>` exactly as on a developer machine, without
# downloading a .NET SDK per pipeline. Built and pushed by `mise run ci:image`
# (its context is only this file plus mise.toml and mise.lock).
FROM jdxcode/mise:2026.9.18-debian

RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      git curl ca-certificates python3 libatomic1 \
      $(apt-cache search --names-only '^libicu[0-9]+$' | cut -d' ' -f1) \
 && rm -rf /var/lib/apt/lists/*

ENV MISE_DATA_DIR=/opt/mise \
    MISE_CACHE_DIR=/opt/mise/cache \
    # The repository is cloned somewhere under /woodpecker at run time.
    MISE_TRUSTED_CONFIG_PATHS=/ \
    MISE_AUTO_INSTALL=false \
    MISE_YES=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    PATH=/opt/mise/shims:$PATH

WORKDIR /opt/ci
COPY mise.toml mise.lock ./
RUN mise install --locked && mise reshim && mise ls --current

WORKDIR /
