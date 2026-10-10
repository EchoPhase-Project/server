# EchoPhase

## Introduction
EchoPhase is a platform for managing bots and integrating with various API services.
It allows you to easily automate tasks, connect different services, and manage the bot lifecycle.

## Commands

Tools, environment and tasks are defined for [mise](https://mise.jdx.dev):

| File | What |
|---|---|
| `mise.toml` | tool versions (.NET SDK, Node, helm, kubectl, oras, oasdiff) and `[env]` |
| `mise.lock` | exact versions, download URLs and checksums of those tools |
| `mise-tasks/*.toml` | tasks: `dotnet`, `docker`, `helm`, `registry`, `openapi` |
| `scripts/` | helpers the tasks use (image tag, Helm arguments, registry pruning) |

### Setup

```bash
mise install --locked          # tools exactly as mise.lock records them
eval "$(mise activate zsh)"    # in ~/.zshrc (or bash/fish): tools and [env] in this repo
```

Without shell activation, prefix commands with `mise exec --`.

### Tasks

```bash
mise tasks                     # list all tasks
mise run                       # pick one interactively
mise run <task> --help         # arguments and flags of a task
```

| Task | What |
|---|---|
| `test`, `test:verbose` | .NET tests (`mise run test -- --filter-class '*Argon2*'`) |
| `lint` / `format` | check / apply code style |
| `openapi` | export `artifacts/openapi.json` (skipped while sources are unchanged) |
| `openapi:check [--base tag]` | fail on breaking API changes against a published contract |
| `openapi:publish` | push the contract as `echophase/openapi:<tag>` and `latest` |
| `docker:build [all\|server\|webapp]` | build images |
| `docker:push [component] [--tag t]` | push with the image tag and `latest` |
| `docker:publish [component] [--tag t]` | build and push |
| `docker:info`, `image-tag` | registry, image names, this checkout's tag |
| `docker:clean`, `clean` | remove local images / build outputs |
| `check` | build both images, as CI does |
| `helm:upgrade`, `helm:install` | deploy this checkout's tag (images must be published) |
| `helm:diff`, `helm:template` | server-side dry run / local render |
| `helm:status`, `helm:history`, `helm:rollback [rev]` | release state and rollback |
| `helm:uninstall`, `helm:delete`, `rebuild` | destructive, ask for confirmation |
| `registry:install` | in-cluster registry (`infra/registry`) |
| `registry:prune [--keep n] [--dry-run]` | delete old tags, then garbage-collect |
| `registry:migrate [--from host:port]` | copy all server/webapp tags from another registry |
| `k3s:image-prune` | unused images in k3s's containerd (sudo) |
| `isopoh:pack [--version v]` | pack Isopoh.Cryptography.Argon2 into `src/packages` |

### Environment

`[env]` in `mise.toml` sets these unless they are already set, so one-off
overrides work (`DOCKER_REGISTRY=registry.example mise run docker:publish`):

| Variable | Default |
|---|---|
| `DOCKER_REGISTRY` | `localhost:30500` |
| `SERVER_IMAGE`, `WEBAPP_IMAGE`, `OPENAPI_REPO` | `echophase/server`, `echophase/webapp`, `echophase/openapi` |
| `HELM_RELEASE`, `HELM_NAMESPACE` | `echophase` |
| `HELM_VALUES`, `HELM_SECRETS` | `values.k3s.yaml`, `secrets.yaml` (in `helm/`, used when present) |
| `KUBECONFIG` | `~/.kube/config` |
| `ISOPOH_DIR` | `../Isopoh.Cryptography.Argon2/Isopoh.Cryptography.Argon2` |
| `IMAGE_TAG` | not set: computed from git (see below) |

### Image tags

Without `--tag` or `IMAGE_TAG`, images get one from git
(`scripts/image-tag.sh`; `mise run image-tag` prints it):

| Checkout | Tag |
|---|---|
| on release tag `v1.2.0` | `1.2.0` |
| 3 commits after `v1.2.0` | `1.2.0-3-g938156d` |
| no release tag yet | `0.0.0-g938156d` |
| uncommitted changes | the above + `-dirty.20261010T153012` (newest change) |

`helm:upgrade` deploys that same tag, so pods roll on every new build and
`helm:rollback` restores the previous images too. It refuses to run until the
images for the tag are in the registry (`mise run docker:publish`).

### Local registry

Inside k3s (`infra/registry/`): `localhost:30500` on the node only,
`registry.registry.svc:5000` for pods.

```bash
mise run registry:install
mise run docker:publish && mise run helm:upgrade
mise run registry:prune --dry-run      # what would be deleted
```

Credentials for the chart go in `helm/secrets.yaml` (gitignored; copy
`helm/secrets.example.yaml`).

### CI and releases

GitHub Actions (`.github/workflows/test.yml`) checks every push and PR on Linux,
Windows and macOS. Woodpecker in the local k3s (`.woodpecker/release.yaml`)
does what needs the registry and the cluster: on pushes to `main` and manual
runs it builds the images, publishes the API contract (failing on breaking
changes) and deploys.

```
GitHub ─webhook─▶ tailscale funnel :8443 /api/hook ─▶ Woodpecker (k3s)
you (tailnet) ──▶ tailscale serve  :9443 (UI, API)  ─▶ Woodpecker
```

Setup, once:

1. Tailscale admin: enable HTTPS certificates and the `funnel` node attribute;
   then `sudo tailscale set --operator=$USER`.
2. GitHub OAuth app with callback `https://<node>.<tailnet>.ts.net:9443/authorize`
   (not 443: k3s's Traefik takes 443 on every node address, the tailscale one too).
3. `cp mise.local.toml.example mise.local.toml` and fill it in.
4. `mise run woodpecker:install && mise run tailscale:expose`
5. In the Woodpecker UI: enable the repository and mark it trusted
   (privileged image builds, the deploy step's ServiceAccount).

Then:

```bash
mise run ci:run                    # release main now
mise run ci:run --no-deploy        # images and contract only
mise run ci:image                  # rebuild the step image after mise.lock changes
```

## Project Structure

```
.
├── webapp/           # Web application (SolidJS + nginx)
├── src/              # Backend application
├── tests/            # Backend tests
├── helm/             # Helm chart of the application
├── infra/            # Cluster infrastructure (registry, Woodpecker, CI image)
├── .woodpecker/      # Woodpecker pipelines
├── mise-tasks/       # Tasks (mise run <task>)
├── scripts/          # Helpers used by the tasks
├── mise.toml         # Tools and environment
└── mise.lock         # Locked tool versions and checksums
```
