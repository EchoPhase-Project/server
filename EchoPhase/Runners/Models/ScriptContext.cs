using EchoPhase.Interfaces;

namespace EchoPhase.Runners.Models
{
    public class ScriptContext : IScriptContext
    {
        public required IDiscordClient DiscordClient
        {
            set; get;
        }
    }
}
