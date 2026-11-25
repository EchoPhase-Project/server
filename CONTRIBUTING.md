# Contributing to the EchoPhase server

Thank you for your help with the server: the API, the realtime gateway, the
workflow runner, and the command line. This guide tells how to start, how
the code is organised, and how to send a change. The rules for the code are
in `docs/contributing/`. The rules for all repositories (issues,
licence, security reports) are in the
[guide of the organisation](https://github.com/EchoPhase-Project/.github/blob/main/CONTRIBUTING.md).

## Preparation

The project pins the tools with [mise](https://mise.jdx.dev): the .NET SDK,
oras, and oasdiff. CI uses the same versions.

```sh
mise install --locked
dotnet tool restore
mise run test
```

The tests do not need a database or Docker. They use EF InMemory and examine
the SQL and the CQL that the code sends. One test runs against a real
Postgres database if `ECHOPHASE_TEST_POSTGRES` contains a connection string.
The test applies migrations to that database. If the variable is empty, the
test does not run.

### Run the server

The server needs PostgreSQL, ScyllaDB, and Valkey (or Redis). The fastest
method is `compose.dev.yaml` from
[charts](https://github.com/EchoPhase-Project/charts). It publishes the
databases on `127.0.0.1`. Then, run this command from `src/EchoPhase.Server`.
The configuration paths are relative to this directory.

```sh
dotnet run -- migrate --continue    # apply the migrations, then start
```

The server reads the settings in this sequence:

1. `Configurations/appsettings.json`.
2. `appsettings.<Environment>.json`.
3. `Configurations/secrets.json`. This file is optional, and git ignores it.
   Put your local passwords in it.
4. The environment variables (`POSTGRES_*`, `VALKEY_*`, `SCYLLA_*`).

`dotnet run -- check` validates the settings and makes a connection to each
service. To run the web client against this server, set
`API_TARGET=http://localhost:5000` in
[webapp](https://github.com/EchoPhase-Project/webapp).

### Before a pull request

```sh
mise run lint             # code style (mise run format corrects it)
mise run test
mise run openapi:check    # if you changed the API
mise run migrations:check # if you changed the database model
```

CI runs these checks on Linux, Windows, and macOS. CI also builds the image.

## How the code is organised

The solution is `EchoPhase.slnx`. The projects are in `src/`. Their tests are
in `tests/` (`EchoPhase.<Project>.Tests`).

| Projects | Contents |
|---|---|
| `Types`, `Configuration` | Shared foundations: results and error codes, options and their validation |
| `DAL.Abstractions`, `DAL.Postgres`, `DAL.Scylla`, `DAL.Redis` | Data access: Postgres (EF Core) for accounts and workflows, Scylla for block runs, Redis for shared state |
| `Security.*`, `Identity` | Cryptography, password hashes, authentication, authorisation (permissions), antiforgery, users and roles |
| `Scripting`, `Runners` | The expression engine and the workflow block runner |
| `Clients`, `Connections`, `WebHooks`, `WebSockets` | External services (Discord, Twitch), connected accounts, webhooks, the realtime gateway |
| `Cli` | The admin commands |
| `Server` | The host: controllers, the service registration, OpenAPI |

Keep the direction of the dependencies. Foundations do not refer to projects
above them. Libraries do not refer to `Server`. Only `Server` connects all
parts. No tool examines this rule, so the reviewers examine it.

## The rules

The rules for the code are in `docs/contributing/`, one file for each
topic:

| File | Topic |
|---|---|
| [code-style.md](docs/contributing/code-style.md) | The style, null values, package versions, the time |
| [api.md](docs/contributing/api.md) | Results and errors, an API endpoint, the API contract |
| [blocks-and-commands.md](docs/contributing/blocks-and-commands.md) | A workflow block, an admin command |
| [configuration.md](docs/contributing/configuration.md) | Settings and telemetry |
| [database.md](docs/contributing/database.md) | Postgres and Scylla migrations |
| [tests.md](docs/contributing/tests.md) | The tests and their names |

The reasons for the large decisions are in [docs/adr/](docs/adr/).

A test (`DocLinksTests`) examines the links in these files. If a link goes to
a file or a heading that is not there, the test fails.

## Commits and pull requests

Commit messages follow Conventional Commits. CI examines them with cocogitto.
The version and the release notes come from them. To examine the messages
when you commit, run `cog install-hook commit-msg`.

- Use `feat` for a new capability and `fix` for a bug fix. Use `refactor` or
  `perf` for other code changes. Use `docs`, `test`, `ci`, `build`, or
  `chore` for all other changes.
- Mark an incompatible change with `!` (`feat!: …`). Before 1.0, only a
  `feat` commit starts a new series. If a `fix!:` commit is alone, the
  contract check fails.
- The scope is the area: `feat(cli): …`, `fix(auth): …`.

The maintainers make the releases with tags `vX.Y.Z`. A tag publishes the
image and the API contract to GHCR, and makes a GitHub release.
