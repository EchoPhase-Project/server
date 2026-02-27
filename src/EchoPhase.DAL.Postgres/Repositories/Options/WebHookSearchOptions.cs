using EchoPhase.DAL.Postgres.Models;

namespace EchoPhase.DAL.Postgres.Repositories.Options
{
    public class WebHookSearchOptions
    {
        public HashSet<Guid>? Ids { get; set; } = null;
        public HashSet<Guid>? UserIds { get; set; } = null;
        public HashSet<string>? Urls { get; set; } = null;
        public WebHookStatus? Status { get; set; } = null;
        public HashSet<string>? Intents { get; set; } = null;
        public HashSet<string>? Names { get; set; } = null;
    }
}
