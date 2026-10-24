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
| `coverage:html` | the same as a browsable report: `artifacts/coverage/html/index.html` |
| `lint` / `format` | check / apply code style |
| `openapi` | export `artifacts/openapi.json` (skipped while sources are unchanged) |
| `migrations:add <name>`, `migrations:check`, `migrations:script` | Postgres migrations (EF Core; `dotnet-ef` from `.config/dotnet-tools.json`) |
| `openapi:check` | fail on breaking API changes within this version's series (below) |
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
`docker exec -i <server> dotnet EchoPhase.Server.dll <command>`, or
`kubectl exec -i deploy/echophase-server -c server -- dotnet EchoPhase.Server.dll <command>`.

| Command | What |
|---|---|
| `check` | validate the settings, reach Postgres, Scylla and Redis, no pending migrations; exit 1 otherwise |
| `migrate [--status]` | apply the Postgres and Scylla migrations, or only list what is pending |
| `users [-s text] [-r role] [--locked]` | list users (roles, lockouts, 2FA), a page at a time — see below |
| `roles` | list roles and what each grants |
| `user <name> [show] [--json]` | account state, roles, grants, what the user owns |
| `user <name> create <full name> [roles]` | create a user; the password from stdin, or asked for |
| `user <name> roles add\|remove <roles>` | change a user's roles |
| `user <name> grants [list\|add <resource> <actions> --reason r [--for 30d]\|remove <resource>]` | permissions beyond the roles; role-only ones are refused |
| `user <name> password` | new password from stdin (or asked for); ends their sessions |
| `user <name> lock [--for 7d]`, `unlock` | lock the account (ending its sessions), or lift a lockout |
| `user <name> delete [--confirm]` | delete the user and everything they own; asks to type the name unless `--confirm`, refuses the only admin |
| `user <name> sessions [--revoke-all]` | list or end a user's sessions |
| `user <name> apiclients list\|create <client> [-p resource:actions] [--expires-in days]\|delete <id>` | API clients; `create` prints the secret once |
| `workflows [list] [-u user] [--active]` | list workflows with their next scheduled run, a page at a time |
| `workflow <id> export`, `workflows import [file] -u user [--name n]` | a workflow as JSON (`{ name, program }`), and back as a new inactive one |
| `workflow <id> enable\|disable` | turn its triggers on or off; the server applies it within a minute |
| `impersonations [-u user] [--active]` | who acted as whom and why, newest first |
| `prune [--older-than 90d] [--dry-run]` | delete idle sessions, old rotated refresh tokens, long-ended impersonations and grants, expired storage values (for a CronJob) |
| `maintenance on [-m message] [--for 30m]`, `off`, `status` | maintenance mode, see below |
| `healthcheck [--url u]` | exit 0 when the server answers |

Lists (`users`, `workflows`, `impersonations`) show 50 rows (`-n` up to 1000) and
how many match; the next page is `--after <cursor>`, the cursor printed under the
table. `--json` prints one object per line (NDJSON) and the next cursor on stderr;
`--json --all` streams every row in batches of 500, for `jq` or a file:

```sh
kubectl exec -i deploy/echophase-server -c server -- dotnet EchoPhase.Server.dll users --json --all | jq -r 'select(.roles | index("admin")) | .userName'
```

Logs of a command go to stderr (warnings and errors only), so stdout is its output.

Exit codes: 0 done, 1 failed (with the reason).

### Maintenance mode

A switch, not configuration: no commit, no deploy. The state is a Redis key
that every instance reads (at most every 5 s), so it takes effect everywhere
within seconds:

```bash
kubectl exec -i deploy/echophase-server -c server -- dotnet EchoPhase.Server.dll maintenance on -m "Database upgrade" --for 30m
kubectl exec -i deploy/echophase-server -c server -- dotnet EchoPhase.Server.dll maintenance off
```

While it is on, `/api/*` answers `503` with `Retry-After` and the error code
`Maintenance`, and the webapp shows its maintenance screen. Still open:
`GET /api/v1/status` (anyone: whether it is on, until when), signing in, and
everything for holders of `maintenance:update` (admins), so they can check
their work first. `--for` ends it by itself; without it, it lasts until `off`.
Admins can also switch it with `PUT`/`DELETE /api/v1/maintenance`.

### Breaking API changes

The contract may not break clients within a compatibility series: a major
version (`1.x`), or before 1.0 a minor one (`0.2.x`, as semver allows 0.x to
break with every minor). `openapi:check` and the release pipelines compare it
with the newest release of its series (`scripts/contract-check.sh`).

To break the API on purpose, the next release must start a new series, and
the check reads which one from the commits since the last tag, the way
`cog bump --auto` will:

- from 1.0, a breaking commit (`fix!: …`, or a `BREAKING CHANGE:` footer)
  makes the next release a new major;
- before 1.0, cocogitto ignores the `!`: only a `feat` (`feat!: …`) makes the
  next release a new minor. A breaking `fix!:` or `refactor!:` alone keeps the
  series, so the check fails; add a `feat!:` or release with `cog bump --minor`.

In a new series breaking changes are allowed; the check lists them against
the previous series.

An enum whose values grow with the product (block types) is marked
`[ExtensibleEnum]`: the contract lists it as `x-extensible-enum`, so a new
value is compatible (a removed one is still breaking) and clients must expect
values they do not know.

A deliberate break within a series (clients already updated) or a false alarm
can be accepted instead: add its line from the check's output (method, path,
message) to `openapi.err-ignore`, with the reason, and empty the file after the
release.

## Telemetry

Traces and metrics go out over OTLP when an endpoint is set with the standard
OpenTelemetry variables; without one nothing is collected. For example
(the project's cluster, see EchoPhase-Project/deploy):

```sh
OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf
OTEL_EXPORTER_OTLP_TRACES_ENDPOINT=http://tempo.monitoring:4318/v1/traces
OTEL_EXPORTER_OTLP_METRICS_ENDPOINT=http://victoria-metrics.monitoring:8428/opentelemetry/v1/metrics
```

Traces: requests (health checks left out), outgoing HTTP, Npgsql queries,
and a span per block run (`EchoPhase.Blocks`) with its calls nested in it.
Metrics: the same, the .NET runtime, and `echophase.block_runs`,
`echophase.block_run.duration` and `echophase.block_run.steps` by status
and trigger.

Errors go to a Sentry-compatible service (the project runs GlitchTip) when
`SENTRY_DSN` is set: unhandled exceptions and error logs, with the logs before
them as breadcrumbs; no traces and no personal data (user, IP, cookies,
request bodies).

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
