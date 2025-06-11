using EchoPhase.Enums;

namespace EchoPhase.Repositories.Options
{
    public class WebHookSearchOptions
    {
        public ISet<Guid>? Ids { get; set; } = null;
        public ISet<Guid>? UserIds { get; set; } = null;
        public ISet<string>? Urls { get; set; } = null;
        public WebHookStatus? Status { get; set; } = null;
        public ISet<string>? Intents { get; set; } = null;
        public ISet<string>? Names { get; set; } = null;
    }
}
