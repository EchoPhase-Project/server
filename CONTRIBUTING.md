# Contributing to the EchoPhase server

Thank you for your help with the server: the API, the realtime gateway, the
workflow runner, and the command line. This guide tells how the code is
organised and which rules it obeys. The rules for all repositories (issues,
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
| `Types`, `Projection`, `Configuration` | Shared foundations: results and error codes, `[Expose]` projection, options and their validation |
| `DAL.Abstractions`, `DAL.Postgres`, `DAL.Scylla`, `DAL.Redis` | Data access: Postgres (EF Core) for accounts and workflows, Scylla for block runs, Redis for shared state |
| `Security.*`, `Identity` | Cryptography, password hashes, authentication, authorisation (permissions), antiforgery, users and roles |
| `Scripting`, `Runners` | The expression engine and the workflow block runner |
| `Clients`, `Connections`, `WebHooks`, `WebSockets` | External services (Discord, Twitch), connected accounts, webhooks, the realtime gateway |
| `Cli` | The admin commands |
| `Server` | The host: controllers, the service registration, OpenAPI |

Keep the direction of the dependencies. Foundations do not refer to projects
above them. Libraries do not refer to `Server`. Only `Server` connects all
parts. No tool examines this rule, so the reviewers examine it.

## Code style

`.editorconfig` contains the rules. `mise run lint` examines them in CI. These
rules are frequently unexpected:

- Each file starts with the licence header. The IDE adds it from
  `.editorconfig`:

  ```csharp
  // Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
  // See the LICENCE file in the repository root for full licence text.
  ```

- Use block-scoped namespaces. The namespace must agree with the folder. Put
  each brace on a separate line.
- Use explicit constructors that set `_camelCase` fields. Do not use primary
  constructors. Do not use `this.`.
- Use `var` only where the type is clear. Do not use `var` for built-in
  types.
- Lines have a maximum of 120 characters, with LF line endings.
- Write a `<summary>` for each public member. On API request and response
  records, the `/// <param>` text becomes the field description in the
  OpenAPI contract.

### Null

Nullable reference types are on in all projects, and their warnings cause the
build to fail. Do not ignore null values. Do not use the null-forgiving
operator (`x!`, `null!`, `default!`). `NullForgivingTests` examines `src` and
`tests` for it. Use one of these methods instead:

- Do a check, with an exception that tells what is missing:
  `entity ?? throw new InvalidOperationException("…")`. You can also use a
  pattern: `if (result is not { } value) return …;`.
- Give the information to the compiler. `IServiceResult<T>.Successful` has
  `[MemberNotNullWhen(true, nameof(Value))]`. `Try…` methods have
  `[NotNullWhen]` or `[MaybeNullWhen]`. Then a result after a check does not
  need `!`.
- Make the type correct. A value that can be missing is `T?`. A member that
  must have a value is `required`. A `DbSet` is `=> Set<T>()`.
- In tests, use `Assert.NotNull(x)` (or `Must.Present(x)` of the project) and
  `result.Ok()`. They fail with the reason.

The project sets package versions centrally, in `src/Directory.Packages.props`
and `tests/Directory.Packages.props`. Do not set versions in a project file.

Get the time from an injected `TimeProvider`. Do not use `DateTime.UtcNow`.
Then the tests can control the time.

## Patterns

### Results and errors

Services return `IServiceResult` or `IServiceResult<T>`
(`EchoPhase.Types/Result`). They do not throw exceptions. Use `Success` and
`Failure`. Use `Map` and `Bind` to chain steps: after a failure, the
subsequent steps do not run. Exceptions are for bugs only. Do not use them
for results that the caller must handle.

An error has a stable code and a message. Codes are `const string` values in
the `*Errors` class of the area (`AuthErrors`, `ApiClientErrors`,
`BlockRunErrors`). If a code in `ErrorCodes` (`NotFound`, `Invalid`,
`Conflict`, `Forbidden`…) has the same meaning, use it again. Clients use the
codes in their logic. Thus, do not change the name of a released code.

### An API endpoint

Use an existing controller as the reference
(`Controllers/Api/v1/Storage/StorageController.cs`):

- The controller derives from `ApiControllerBase`. It is in
  `Controllers/Api/v1/<Area>/`.
- Request and response types are `sealed record` types with `/// <param>` docs
  and validation attributes.
- Each action has `[RequirePermission(Resources.X, Permissions.Y)]`. Each
  action that changes data also has `[ValidateAntiForgery]`.
- Each status code has `[ProducesResponseType]`. Errors use `ErrorBody`.
- Each action gives its `CancellationToken` to the calls that it makes.

`Failure(error)` changes a service failure into a response.
`ApiControllerBase.StatusFor` sets the status code. **If a new error code must
not return 400, add its case to `StatusFor`.** If you do not add it, the code
returns 400.

Return entities through the `Projector`. Only the properties with `[Expose]`
go out of the server.

Permissions are `resource:action` constants (`Security.BitMasks/Constants`).
`RolePermissions.cs` gives them to roles. A new permission changes the
permission encoding in tokens. Tell this in the pull request.

### The API contract

The web client and other clients use the contract (`artifacts/openapi.json`,
`mise run openapi`). Nullability is important: a reference type that is not
nullable is a required field.

The contract must stay compatible with clients in a release series. Before
1.0, a series is a minor version (`0.2.x`). `mise run openapi:check` compares
the contract with the latest release of its series. It fails if it finds an
incompatible change.

If you must make an incompatible change, the commit must start a new series.
Before 1.0, this is a `feat!:` commit. For an intentional exception, add an
entry with its reason to `openapi.err-ignore`. The README tells about the two
methods in "Breaking API changes".

If more values can be added to an enum in the future, give it
`[ExtensibleEnum]`. Then a new value is a compatible change, and clients must
accept values that they do not know.

### A workflow block

1. Add a value to `BlockTypes` (`Runners/Blocks/BlockTypes.cs`).
2. Add the parameters in `Params/<X>Params.cs`. The class implements
   `IValidatable`. Each property has `[JsonPropertyName]`.
3. Add the handler in `Handlers/<X>Handler.cs`. The class derives from
   `BlockHandlerBase<XParams>` and has `[BlockType(...)]` with its
   description.

Handlers register automatically. A test fails if a block type does not have a
handler.

### A command

Commands are Spectre.Console `AsyncCommand<TSettings>` classes in
`Cli/Commands/<Area>/`. Register each command in
`Server/Extensions/HostExtensions.cs` with a description and examples. A test
validates the examples.

- A command returns `CliExit.Success` or `CliExit.Failure` (exit code 1).
- A command writes its output to stdout and its logs to stderr. Then users
  can send the output to other programs. Keep this behaviour.
- For a list, add the `--json` option.

All commands are in `EchoPhase.Cli`. Do not put a command in `Server`:
`CliCommandPlacementTests` fails. If a command needs a service that only
`Server` has, define an interface in `Cli/Commands/<Area>/` with only the
operations that the command uses. Implement the interface in `Server` and
register it in `Startup.cs`. `IWorkflowEditor` and `WorkflowEditor` show this
pattern.

### Settings

An options class has a `SectionName` and a validator. Register them in
`Configuration/Extensions/ServiceExtensions.cs`. Then the server does not
start with incorrect settings, and `check` shows them.

If a self-hoster needs a setting, also write about it in the chart
([charts](https://github.com/EchoPhase-Project/charts)).

### Telemetry

Traces and metrics are on only when an OTLP endpoint is set. For new traces
or metrics, use the current source and meter, or register a new one in
`TelemetryExtensions.cs`. Metric names and attribute names start with
`echophase.`.

## Database changes

**Postgres.** Change the model in `DAL.Postgres`. Then run:

```sh
mise run migrations:add <Name>
```

`migrations:check` fails if the model has changes that no migration includes.
Do not edit a released migration. Add a new migration.

**Scylla.** Write migrations manually in `DAL.Scylla/Migrations/`. Each
migration is one class `M<yyyyMMdd><Name>` that implements `IMigration`. Give
it a unique `Id` that sorts after the current `Id` values. CQL schema changes
are not transactional. Thus, `Down` must undo a partial `Up` (`IF EXISTS`).

The `migrate` command applies the two types of migrations. The chart runs
this command before the server starts.

## Tests

The tests use xUnit v3 on Microsoft Testing Platform. Each source project has
one test project.

```sh
mise run test                                   # all tests
mise run test -- --filter-class '*Argon2*'      # some tests
mise run test:coverage                          # artifacts/coverage
```

Name test classes `<Subject>Tests`. Name test methods as a sentence:
`Member_Condition_Outcome` (`Failure_SkipsTheStepsAndKeepsItsError`). Test the
behaviour that a caller sees, including the failures.

The expression engine has allocation budgets (`AllocationBudgetTests`). If a
change makes more allocations, these tests fail.

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
