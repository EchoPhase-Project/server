using EchoPhase.Clients.Discord;

namespace EchoPhase.Runners.Roslyn.Contexts
{
    public interface IScriptContext
    {
        public IDiscordClient DiscordClient
        {
            get;
        }
    }
}
