# EchoPhase server

EchoPhase is a platform for managing bots and integrating with various API
services: automate tasks, connect services, manage the bot lifecycle. This
repository is the server (.NET): REST API, realtime gateway, workflows.

| Repository | What |
|---|---|
| **server** (here) | API, gateway, workflows; the `server` image and the API contract |
| [webapp](https://github.com/EchoPhase-Project/webapp) | web client (SolidJS); the `webapp` image |
| [charts](https://github.com/EchoPhase-Project/charts) | Helm chart and Docker Compose to run EchoPhase yourself |
| [deploy](https://github.com/EchoPhase-Project/deploy) | the project's own instance (Flux, cluster infrastructure) |

## Running it

Published images and the chart are on GHCR; see
[charts](https://github.com/EchoPhase-Project/charts) for Helm and Compose.

## Development

Tools, environment and tasks are defined for [mise](https://mise.jdx.dev):

```bash
mise install --locked          # .NET SDK, oras, oasdiff, woodpecker-cli as in mise.lock
eval "$(mise activate zsh)"    # in ~/.zshrc (or bash/fish)
mise tasks                     # everything below
```

| Task | What |
|---|---|
| `test`, `test:verbose` | tests (`mise run test -- --filter-class '*Argon2*'`) |
| `test:coverage` | tests with coverage, Cobertura reports in `artifacts/coverage` |
| `lint` / `format` | check / apply code style |
| `openapi` | export `artifacts/openapi.json` (skipped while sources are unchanged) |
| `migrations:add <name>`, `migrations:check`, `migrations:script` | Postgres migrations (EF Core; `dotnet-ef` from `.config/dotnet-tools.json`) |
| `openapi:check [--base tag]` | fail on breaking API changes against a published contract |
| `openapi:publish` | push the contract to `$DOCKER_REGISTRY/echophase/openapi:<tag>` and `latest` |
| `docker:build`, `docker:push [--tag t]`, `docker:publish` | the server image |
| `image-tag`, `docker:info` | this checkout's image tag (see below) |
| `isopoh:pack --isopoh-dir <checkout> [--version v]` | pack [Isopoh.Cryptography.Argon2](https://github.com/EchoPhase-Project/Isopoh.Cryptography.Argon2) (branch `echophase`) into `src/packages` |
| `ci:image`, `ci:run` | Woodpecker step image, manual pipeline run |

Without `--tag` or `IMAGE_TAG`, images get a tag from git
(`scripts/image-tag.sh`): `1.2.0` on tag `v1.2.0`, `1.2.0-3-g938156d` after it,
`0.0.0-g938156d` before any release, plus `-dirty.<time>` for uncommitted
changes.

## Command line

The server binary doubles as an admin tool: with arguments it runs the command
and exits (`--continue` starts the server afterwards). In a container:
`docker exec -i <server> dotnet EchoPhase.dll <command>`, or
`kubectl exec -i deploy/echophase -c server -- dotnet EchoPhase.dll <command>`.

| Command | What |
|---|---|
| `migrate [--status]` | apply the Postgres and Scylla migrations, or only list what is pending |
| `users`, `roles` | list users (roles, lockouts); list roles and what each grants |
| `user <name> create <full name> <password> [roles]` | create a user |
| `user <name> roles add\|remove <roles>` | change a user's roles |
| `user <name> password` | new password from stdin (or asked for); ends their sessions |
| `user <name> unlock` | lift a lockout after failed sign-ins |
| `user <name> sessions [--revoke-all]` | list or end a user's sessions |
| `user <name> apiclients list\|create <client> [-p resource:actions] [--expires-in days]\|delete <id>` | API clients; `create` prints the secret once |
| `healthcheck [--url u]` | exit 0 when the server answers |

Exit codes: 0 done, 1 failed (with the reason).

## CI and releases

- **GitHub Actions** (`.github/workflows/test.yml`): every push and PR, tests
  on Linux, Windows and macOS, formatting, OpenAPI export, image build;
  coverage from the Linux run goes to
  [Codecov](https://app.codecov.io/gh/EchoPhase-Project/server).
- **Tags `vX.Y.Z`** (`publish.yml`): the `server` image and the API contract
  to `ghcr.io/echophase-project` (for self-hosting).
- **Woodpecker** (`.woodpecker/release.yaml`, runs in the project's own
  cluster): on `main` builds the image into the in-cluster registry, publishes
  the contract, and commits the new tag to
  [deploy](https://github.com/EchoPhase-Project/deploy), from where Flux
  deploys it.
