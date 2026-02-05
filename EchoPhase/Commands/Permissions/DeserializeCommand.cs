using System.Text.Json;
using EchoPhase.Commands.Settings;
using EchoPhase.Extensions;
using EchoPhase.Interfaces;
using EchoPhase.Services.Bitmasks;
using Spectre.Console;
using Spectre.Console.Cli;

namespace EchoPhase.Commands
{
    public class PermissionsDeserializeCommand : Command<PermissionsDeserializeCommandSettings>
    {
        private readonly IPermissionsBitMaskService _permissionsService;

        public PermissionsDeserializeCommand(IPermissionsBitMaskService permissionsService)
        {
            _permissionsService = permissionsService;
        }

        public override int Execute(CommandContext context, PermissionsDeserializeCommandSettings settings, CancellationToken cancellationToken)
        {
            var deserialized = PermissionsBitMaskService.Deserialize(settings.Permissions);

            if (deserialized.TryGetError(out var err))
            {
                AnsiConsole.MarkupLine($"[red]{err.Value}[/]");
                return -1;
            }

            if (deserialized.TryGetValue(out var r))
            {
                var decoded = _permissionsService.Decode(r);

                if (decoded.TryGetError(out err))
                {
                    AnsiConsole.MarkupLine($"[red]{err.Value}[/]");
                    return -1;
                }

                if (decoded.TryGetValue(out var dict))
                    Console.WriteLine(JsonSerializer.Serialize(dict));
            }

            return 1;
        }
    }
}
