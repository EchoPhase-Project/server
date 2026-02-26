using System.Text.Json;
using EchoPhase.Security.BitMasks;
using EchoPhase.Types.Result.Extensions;

namespace EchoPhase.Cli.Commands.Intents.Serialize
{
    public class SerializeCommand : Command<SerializeSettings>
    {
        private readonly IIntentsBitMask _intents;

        public SerializeCommand(IIntentsBitMask intents)
        {
            _intents = intents;
        }

        public override int Execute(CommandContext context, SerializeSettings settings, CancellationToken cancellationToken)
        {
            var encoded = _intents.Encode(settings.Intents);

            if (encoded.TryGetError(out var err))
            {
                AnsiConsole.MarkupLine($"[red]{err.Value}[/]");
                return 1;
            }

            if (encoded.TryGetValue(out var mask))
            {
                var serialized = IntentsBitMask.Serialize(mask);

                if (serialized.TryGetError(out err))
                {
                    AnsiConsole.MarkupLine($"[red]{err.Value}[/]");
                    return 1;
                }

                if (serialized.TryGetValue(out var r))
                    Console.WriteLine(JsonSerializer.Serialize(r));
            }

            return 0;
        }
    }
}
