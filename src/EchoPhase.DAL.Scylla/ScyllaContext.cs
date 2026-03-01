using EchoPhase.Configuration.Database.Scylla;
using Microsoft.Extensions.Options;

namespace EchoPhase.DAL.Scylla
{
    public class ScyllaContext : DbContext
    {
        public ScyllaContext(IOptions<ScyllaOptions> options) : base(options.Value)
        {
        }

        protected override void OnModelBuilding(ModelBuilder builder)
        {
            base.OnModelBuilding(builder);
        }
    }
}
