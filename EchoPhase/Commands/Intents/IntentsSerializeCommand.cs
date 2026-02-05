using System.Text.Json;
using EchoPhase.Commands.Settings;
using EchoPhase.Extensions;
using EchoPhase.Interfaces;
using EchoPhase.Services.Bitmasks;
using Spectre.Console;
using Spectre.Console.Cli;

namespace EchoPhase.Commands
{
    public class IntentsSerializeCommand : Command<IntentsSerializeCommandSettings>
    {
        private readonly IIntentsBitMaskService _intentsService;

        public IntentsSerializeCommand(IIntentsBitMaskService intentsService)
        {
            _intentsService = intentsService;
        }

        public override int Execute(CommandContext context, IntentsSerializeCommandSettings settings, CancellationToken cancellationToken)
        {
            var encoded = _intentsService.Encode(settings.Intents);

            if (encoded.TryGetError(out var err))
            {
                AnsiConsole.MarkupLine($"[red]{err.Value}[/]");
                return 1;
            }

            if (encoded.TryGetValue(out var mask))
            {
                var serialized = IntentsBitMaskService.Serialize(mask);

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
