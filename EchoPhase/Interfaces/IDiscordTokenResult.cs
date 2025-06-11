using EchoPhase.Models;

namespace EchoPhase.Interfaces
{
    public interface IDiscordTokenResult
    {
        public ISet<DiscordToken> Affected
        {
            get; set;
        }
        public ISet<string> Errors
        {
            get; set;
        }
        public bool IsSucceeded
        {
            get; set;
        }
    }
}
