using EchoPhase.Clients.Discord;

namespace EchoPhase.Runners.Roslyn.Contexts
{
    public class ScriptContext : IScriptContext
    {
        public required IDiscordClient DiscordClient
        {
            set; get;
        }
    }
}
