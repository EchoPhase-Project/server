# AGENTS.md: EchoPhase server

This repository contains the .NET 10 server: the REST API, the realtime
gateway, the workflow runner, and the admin CLI. The full rules for persons are
in [CONTRIBUTING.md](CONTRIBUTING.md). This file is the short version for
coding agents. If this file and CONTRIBUTING.md are different, CONTRIBUTING.md
and the code are correct. Then correct this file.

## Commands

Get the tools from mise. Run `mise install --locked`, then run
`dotnet tool restore`.

| Task | Command |
|---|---|
| Build the solution | `dotnet build EchoPhase.slnx` |
| Run all tests | `mise run test` |
| Run some tests | `mise run test -- --filter-class '*Argon2*'` |
| Examine the code style | `mise run lint` (to correct the style, run `mise run format`) |
| Export the API contract | `mise run openapi` |
| Find incompatible changes in the contract | `mise run openapi:check` (this task uses the registry, refer to CONTRIBUTING) |
| Add a Postgres migration | `mise run migrations:add <Name>` |
| Make sure that migrations include all model changes | `mise run migrations:check` |

Before you stop, do these steps:

1. Make sure that `mise run lint` and `mise run test` are successful.
2. If you changed the API, run `mise run openapi`.
3. If you changed a Postgres model, add a migration. Make sure that
   `migrations:check` is successful.

The tests do not use a database or Docker. Do not start services for the tests.

## Layout

- `src/EchoPhase.*` contains these projects:
  - Foundations: `Types`, `Projection`, `Configuration`.
  - Data access: `DAL.*` (Postgres, Scylla, Redis).
  - Other libraries: `Security.*`, `Identity`, `Scripting`, `Runners`
    (workflow blocks), `Clients`, `Connections`, `WebHooks`, `WebSockets`,
    `Cli`.
  - The host: `Server` (controllers, service registration, OpenAPI).
- `tests/EchoPhase.<Project>.Tests` contains the tests. The tests use xUnit v3
  on Microsoft Testing Platform.
- Dependencies go in one direction only. Libraries do not refer to `Server`.
  Foundations do not refer to projects above them.

## Rules

### Code style

- Start each `.cs` file with the licence header. The exact text is in
  `.editorconfig`, in `file_header_template`. Migrations do not have this
  header.
- Use block-scoped namespaces. The namespace must agree with the folder.
- Put each brace on a separate line.
- Use explicit constructors with `_camelCase` fields. Do not use primary
  constructors.
- Do not use `var` for built-in types.
- If the code does not obey these rules, `mise run lint` fails.
- Write package versions only in `src/Directory.Packages.props` or
  `tests/Directory.Packages.props`. Do not write package versions in a
  `.csproj` file.

### Services and errors

- Services return `IServiceResult` or `IServiceResult<T>`. Do not throw an
  exception for an expected result.
- Error codes are `const string` values in the `*Errors` class of the area.
  Do not change the name of a released error code.
- If a new error code must not return status 400, add its case to
  `ApiControllerBase.StatusFor`.

### Controllers and the API contract

- Each controller must derive from `ApiControllerBase`.
- Each action must have `[RequirePermission(...)]`.
- Each action that changes data must have `[ValidateAntiForgery]`.
- Declare each status code with `[ProducesResponseType]`.
- Return entities through the `Projector`. Only `[Expose]` properties go out
  of the server.
- Write `/// <param>` docs for request and response records. These docs
  become the descriptions in the contract.
- Nullability is part of the contract.
- Do not make incompatible changes to the API contract in a release series.
- If more values can be added to an enum in the future, give it
  `[ExtensibleEnum]`.

### Workflow blocks and CLI commands

- To add a workflow block, add these items:
  - A `BlockTypes` value.
  - `Params/<X>Params.cs`, which implements `IValidatable`.
  - `Handlers/<X>Handler.cs` with `[BlockType(...)]`.
- To add a CLI command, put the command in `EchoPhase.Cli`. Do not put a
  command in `Server`: `CliCommandPlacementTests` fails.
- If a command needs a service that only `Server` has, define an interface in
  `Cli` and implement it in `Server` (refer to `IWorkflowEditor`).
- Register each command in `Server/Extensions/HostExtensions.cs`. Give it a
  description and examples. Write the output to stdout. Write logs to stderr.

### Time and null values

- Get the time from an injected `TimeProvider`. Do not use
  `DateTime.UtcNow`.
- Do not ignore null values. Do not use `x!`, `null!`, or `default!`.
  Nullable warnings are errors, and `NullForgivingTests` fails if it finds
  one of these forms.
- Use one of these methods instead:
  - Do a check: `?? throw` with a message, or a pattern.
  - Add an annotation: `[MemberNotNullWhen]` or `[NotNullWhen]`.
  - Make the type correct: `T?` or `required`.
- In tests, use `Assert.NotNull`, `Must.Present`, or `result.Ok()`.

### Migrations

- Do not edit a released migration (Postgres or Scylla). Add a new migration.
- Write Scylla migrations manually in `DAL.Scylla/Migrations/`.
- The `Down` method of a Scylla migration must work after a partial `Up`.

### Permissions and secrets

- If you change the `Resources` or `Permissions` constants, the permission
  encoding in tokens changes. Tell the maintainers about this change.
- Do not commit secrets. `Configurations/secrets.json` and `mise.local.toml`
  are local files, and git ignores them.

## Commits

Use Conventional Commits. CI examines the commit messages with cocogitto.

- Use these types: `feat:`, `fix:`, `refactor:`, `perf:`, `docs:`, `test:`,
  `ci:`, `build:`, `chore:`.
- Add a scope if it helps the reader, for example `feat(cli): …`.
- Mark incompatible changes with `!`.
- Before version 1.0, only a `feat` commit starts a new API series. If a
  `fix!:` commit has no `feat` commit with it, the contract check fails.
