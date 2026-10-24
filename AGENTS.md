# AGENTS.md: EchoPhase server

.NET 10 server: REST API, realtime gateway, workflow runner, admin CLI.
Humans read [CONTRIBUTING.md](CONTRIBUTING.md); this file is the short version
for coding agents. When the two disagree, CONTRIBUTING.md and the code win:
fix this file.

## Commands

Tools come from mise (`mise install --locked`, then `dotnet tool restore`).

| Goal | Command |
|---|---|
| Build | `dotnet build EchoPhase.slnx` |
| Test everything | `mise run test` |
| Test some | `mise run test -- --filter-class '*Argon2*'` |
| Check style | `mise run lint` (fix with `mise run format`) |
| Export the API contract | `mise run openapi` |
| Check the contract for breaks | `mise run openapi:check` (needs the registry, see CONTRIBUTING) |
| New Postgres migration | `mise run migrations:add <Name>` |
| Model covered by migrations | `mise run migrations:check` |

Before you finish: `mise run lint` and `mise run test` pass. If you touched
the API, `mise run openapi`; if you touched a Postgres model, a migration
exists and `migrations:check` passes.

Tests need no database or Docker. Do not start services to run them.

## Layout

- `src/EchoPhase.*`: `Types`, `Projection`, `Configuration` (foundations),
  `DAL.*` (Postgres, Scylla, Redis), `Security.*`, `Identity`, `Scripting`,
  `Runners` (workflow blocks), `Clients`, `Connections`, `WebHooks`,
  `WebSockets`, `Cli`, `Server` (host: controllers, wiring, OpenAPI).
- `tests/EchoPhase.<Project>.Tests`: xUnit v3 on Microsoft Testing Platform.
- Dependencies point down: libraries never reference `Server`; foundations
  reference nothing above them.

## Rules

- Every `.cs` file starts with the licence header (exact text in
  `.editorconfig`, `file_header_template`). Migrations are exempt.
- Block-scoped namespaces matching the folder; braces on their own line;
  explicit constructors with `_camelCase` fields (no primary constructors);
  no `var` for built-in types. `mise run lint` fails otherwise.
- Package versions only in `src/Directory.Packages.props` or
  `tests/Directory.Packages.props`, never in a `.csproj`.
- Services return `IServiceResult` / `IServiceResult<T>`; do not throw for
  expected outcomes. Error codes are `const string`s in the area's `*Errors`
  class; never rename a released code.
- A new error code that should not answer 400 needs its case in
  `ApiControllerBase.StatusFor`.
- Controllers: derive from `ApiControllerBase`; every action has
  `[RequirePermission(...)]`; mutating actions have `[ValidateAntiForgery]`;
  declare every status with `[ProducesResponseType]`; return entities through
  the `Projector` (only `[Expose]` properties leave the server).
- Request/response records get `/// <param>` docs: they become the contract's
  descriptions. Nullability is part of the contract.
- Do not break the API contract within a release series. A growing enum gets
  `[ExtensibleEnum]`.
- New workflow block: a `BlockTypes` value, `Params/<X>Params.cs`
  (`IValidatable`), `Handlers/<X>Handler.cs` with `[BlockType(...)]`.
- New CLI command: register it in `Server/Extensions/HostExtensions.cs` with
  description and examples; output on stdout, logs on stderr.
- Read time from an injected `TimeProvider`, not `DateTime.UtcNow`.
- Never edit a released migration (Postgres or Scylla); add a new one. Scylla
  migrations are hand-written in `DAL.Scylla/Migrations/` and their `Down`
  must survive a half-applied `Up`.
- Changing `Resources` or `Permissions` constants changes the permission
  encoding in tokens: call it out.
- Do not commit secrets: `Configurations/secrets.json` and
  `mise.local.toml` are local and gitignored.

## Commits

Conventional Commits, checked in CI (cocogitto): `feat:`, `fix:`,
`refactor:`, `perf:`, `docs:`, `test:`, `ci:`, `build:`, `chore:`, with a scope
where it helps (`feat(cli): …`). Mark breaking changes with `!`. Before 1.0,
only a `feat` starts a new API series: a `fix!:` alone fails the contract
check.
