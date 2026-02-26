using System.Text.Json;
using EchoPhase.Security.BitMasks;
using EchoPhase.Types.Result.Extensions;

namespace EchoPhase.Cli.Commands.Permissions.Deserialize
{
    public class DeserializeCommand : Command<DeserializeSettings>
    {
        private readonly IPermissionsBitMask _permissions;

        public DeserializeCommand(IPermissionsBitMask permissions)
        {
            _permissions = permissions;
        }

        public override int Execute(CommandContext context, DeserializeSettings settings, CancellationToken cancellationToken)
        {
            var deserialized = PermissionsBitMask.Deserialize(settings.Permissions);

            if (deserialized.TryGetError(out var err))
            {
                AnsiConsole.MarkupLine($"[red]{err.Value}[/]");
                return -1;
            }

            if (deserialized.TryGetValue(out var r))
            {
                var decoded = _permissions.Decode(r);

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
