# Workflow blocks and commands

This file tells how to add a workflow block and an admin command.

## A workflow block

1. Add a value to `BlockTypes` (`Runners/Blocks/BlockTypes.cs`).
2. Add the parameters in `Params/<X>Params.cs`. The class implements
   `IValidatable`. Each property has `[JsonPropertyName]`.
3. Add the handler in `Handlers/<X>Handler.cs`. The class derives from
   `BlockHandlerBase<XParams>` and has `[BlockType(...)]` with its
   description.

Handlers register automatically. A test fails if a block type does not have a
handler.


## A command

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
