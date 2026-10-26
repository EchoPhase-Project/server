# Contributing to the EchoPhase server

Thank you for helping with the server: the API, the realtime gateway, the
workflow runner and the command line. This guide covers how the code is
organised and the rules it follows; what is common to every repository
(issues, licence, security reports) is in the
[organisation's guide](https://github.com/EchoPhase-Project/.github/blob/main/CONTRIBUTING.md).

## Setting up

Tools are pinned with [mise](https://mise.jdx.dev): the .NET SDK, oras and
oasdiff, the same versions as in CI.

```sh
mise install --locked
dotnet tool restore
mise run test
```

The tests need no database or Docker: they use EF InMemory and check the SQL
and CQL the code would send. One test runs against a real Postgres when
`ECHOPHASE_TEST_POSTGRES` holds a connection string to a database it may
migrate; otherwise it is skipped.

### Running the server

It needs PostgreSQL, ScyllaDB and Valkey (or Redis). The quickest way is
`compose.dev.yaml` from [charts](https://github.com/EchoPhase-Project/charts),
which publishes them on `127.0.0.1`. Then, from `src/EchoPhase.Server` (the
configuration paths are relative to it):

```sh
dotnet run -- migrate --continue    # apply the migrations, then start
```

Settings come from `Configurations/appsettings.json`, then
`appsettings.<Environment>.json`, then `Configurations/secrets.json` (optional,
gitignored: your local passwords), then the environment (`POSTGRES_*`,
`VALKEY_*`, `SCYLLA_*`). `dotnet run -- check` validates the settings and
reaches every service. Run the web client against it with
`API_TARGET=http://localhost:5000` in [webapp](https://github.com/EchoPhase-Project/webapp).

### Before a pull request

```sh
mise run lint             # code style (mise run format fixes it)
mise run test
mise run openapi:check    # if you changed the API
mise run migrations:check # if you changed the database model
```

CI runs these on Linux, Windows and macOS, and builds the image.

## How the code is organised

The solution is `EchoPhase.slnx`: projects in `src/`, their tests in `tests/`
(`EchoPhase.<Project>.Tests`).

| Projects | What |
|---|---|
| `Types`, `Projection`, `Configuration` | shared foundations: results and error codes, `[Expose]` projection, options and their validation |
| `DAL.Abstractions`, `DAL.Postgres`, `DAL.Scylla`, `DAL.Redis` | data access: Postgres (EF Core) for accounts and workflows, Scylla for block runs, Redis for shared state |
| `Security.*`, `Identity` | cryptography, password hashing, authentication, authorisation (permissions), antiforgery, users and roles |
| `Scripting`, `Runners` | the expression engine and the workflow block runner |
| `Clients`, `Connections`, `WebHooks`, `WebSockets` | outside services (Discord, Twitch), connected accounts, webhooks, the realtime gateway |
| `Cli` | the admin commands |
| `Server` | the host: controllers, services wiring it together, OpenAPI |

Keep the direction: foundations reference nothing above them, libraries never
reference `Server`, and only `Server` puts everything together. Nothing checks
this automatically, so reviewers do.

## Code style

`.editorconfig` holds the rules, and `mise run lint` checks them in CI. The
ones that surprise most:

- every file starts with the licence header (the IDE adds it from
  `.editorconfig`):

  ```csharp
  // Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
  // See the LICENCE file in the repository root for full licence text.
  ```

- block-scoped namespaces matching the folder, braces on their own line;
- explicit constructors assigning `_camelCase` fields (no primary
  constructors), no `this.`;
- `var` only where the type is apparent, never for built-in types;
- lines up to 120 characters, LF endings;
- public members documented with `<summary>`; on API request and response
  records, `/// <param>` becomes the field's description in the OpenAPI
  contract.

### Null

Nullable reference types are on everywhere, and their warnings fail the
build. Ignoring null is not allowed: no null-forgiving operator (`x!`,
`null!`, `default!`), which `NullForgivingTests` checks across `src` and
`tests`. Instead:

- check it, with an exception that says what was missing:
  `entity ?? throw new InvalidOperationException("…")`, or a pattern
  (`if (result is not { } value) return …;`);
- let the compiler know: `[MemberNotNullWhen(true, nameof(Value))]` on
  `IServiceResult<T>.Successful`, `[NotNullWhen]` / `[MaybeNullWhen]` on
  `Try…` methods, so that a checked result needs no `!`;
- make the type honest: a value that may be missing is `T?`; a member that
  must be set is `required`; a `DbSet` is `=> Set<T>()`;
- in tests, `Assert.NotNull(x)` (or the project's `Must.Present(x)`) and
  `result.Ok()`, which fail with the reason.

Packages are versioned centrally: `src/Directory.Packages.props` and
`tests/Directory.Packages.props`, never in a project file. Read the time from
an injected `TimeProvider`, not `DateTime.UtcNow`, so tests can control it.

## Patterns

### Results and errors

Services return `IServiceResult` / `IServiceResult<T>`
(`EchoPhase.Types/Result`) instead of throwing: `Success`, `Failure`, and
`Map` / `Bind` to chain steps (a failure skips the rest). Exceptions are for
bugs, not for outcomes a caller should handle.

An error has a stable code and a message. Codes are `const string`s in the
area's `*Errors` class (`AuthErrors`, `ApiClientErrors`, `BlockRunErrors`),
reusing `ErrorCodes` (`NotFound`, `Invalid`, `Conflict`, `Forbidden`…) where
the meaning is the same. Clients branch on codes, so never rename one that has
been released.

### An API endpoint

Look at an existing controller (`Controllers/Api/v1/Storage/StorageController.cs`):

- a controller deriving from `ApiControllerBase`, under `Controllers/Api/v1/<Area>/`;
- request and response `sealed record`s with `/// <param>` docs and validation attributes;
- `[RequirePermission(Resources.X, Permissions.Y)]` on every action, `[ValidateAntiForgery]` on those that change something;
- `[ProducesResponseType]` for every status, `ErrorBody` for errors;
- a `CancellationToken` passed down.

`Failure(error)` turns a service failure into a response: the status comes
from `ApiControllerBase.StatusFor`. **A new error code that is not a 400 needs
its case there**; otherwise it answers 400. Entities are returned through the
`Projector`: only properties marked `[Expose]` leave the server.

Permissions are `resource:action` constants (`Security.BitMasks/Constants`),
given to roles in `RolePermissions.cs`. Adding one changes how permissions are
encoded in tokens; mention it in the pull request.

### The API contract

The contract (`artifacts/openapi.json`, `mise run openapi`) is what the web
client and other clients are built against. Nullability counts: a non-nullable
reference type is a required field.

It may not break clients within a release series: before 1.0, a minor version
(`0.2.x`). `mise run openapi:check` compares it with the latest release of its
series and fails on breaking changes. If a break is intended, the commit must
start a new series (before 1.0, a `feat!:` commit); a deliberate exception goes
in `openapi.err-ignore` with its reason. The README explains both under
"Breaking API changes".

Enums that grow with the product are marked `[ExtensibleEnum]`: adding a value
is then compatible, and clients must expect values they do not know.

### A workflow block

1. A value in `BlockTypes` (`Runners/Blocks/BlockTypes.cs`).
2. Its parameters in `Params/<X>Params.cs`, implementing `IValidatable`, with
   `[JsonPropertyName]` on each property.
3. Its handler in `Handlers/<X>Handler.cs`, deriving from
   `BlockHandlerBase<XParams>`, with `[BlockType(...)]` describing it.

Handlers register themselves; a test fails when a block type has no handler.

### A command

Commands are Spectre.Console `AsyncCommand<TSettings>` classes in
`Cli/Commands/<Area>/`, registered in `Server/Extensions/HostExtensions.cs`
with a description and examples (the examples are validated). They return
`CliExit.Success` or `CliExit.Failure` (exit code 1). Their output goes to
stdout and logs to stderr, so output can be piped: keep it that way, and offer
`--json` for lists.

All commands live in `EchoPhase.Cli`; `CliCommandPlacementTests` fails on a
command in `Server`. When a command needs a service only `Server` has, define
an interface in `Cli/Commands/<Area>/` with just the operations the command
uses, implement it in `Server` and register it in `Startup.cs`
(`IWorkflowEditor` and `WorkflowEditor`).

### Settings

An options class has a `SectionName` and a validator, registered in
`Configuration/Extensions/ServiceExtensions.cs` so the server refuses to start
with invalid settings and `check` reports them. A setting a self-hoster needs
is documented in the chart ([charts](https://github.com/EchoPhase-Project/charts))
too.

### Telemetry

Traces and metrics are on only when an OTLP endpoint is set. New ones use the
existing source and meter, or register theirs in `TelemetryExtensions.cs`;
metric names start with `echophase.`, attributes with `echophase.`.

## Database changes

**Postgres.** Change the model in `DAL.Postgres`, then:

```sh
mise run migrations:add <Name>
```

`migrations:check` fails when the model has changes no migration covers.
Never edit a migration that has been released: add a new one.

**Scylla.** Migrations are written by hand in `DAL.Scylla/Migrations/`, one
class `M<yyyyMMdd><Name>` implementing `IMigration`, with a unique `Id` that
sorts after the existing ones. CQL schema changes are not transactional:
`Down` must undo a half-applied `Up` (`IF EXISTS`).

Both are applied by the `migrate` command, which the chart runs before the
server starts.

## Tests

xUnit v3 on Microsoft Testing Platform, one test project per source project.

```sh
mise run test                                   # everything
mise run test -- --filter-class '*Argon2*'      # some
mise run test:coverage                          # artifacts/coverage
```

Name test classes `<Subject>Tests` and test methods as a sentence:
`Member_Condition_Outcome` (`Failure_SkipsTheStepsAndKeepsItsError`). Test the
behaviour a caller sees, including failures. The expression engine has
allocation budgets (`AllocationBudgetTests`): a change that allocates more
fails them.

## Commits and pull requests

Commit messages follow Conventional Commits, checked in CI by cocogitto; they
make the version and the release notes. `cog install-hook commit-msg` checks
them as you commit.

- `feat` is a new capability, `fix` a bug fix, `refactor` / `perf` other code
  changes; `docs`, `test`, `ci`, `build`, `chore` the rest.
- A breaking change gets a `!` (`feat!: …`). Before 1.0, only a `feat` starts
  a new series: a `fix!:` alone fails the contract check.
- Scopes name the area: `feat(cli): …`, `fix(auth): …`.

Releases are tags `vX.Y.Z` made by the maintainers: they publish the image and
the API contract to GHCR, with a GitHub release.
