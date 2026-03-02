using EchoPhase.Configuration.Database;
using Microsoft.Extensions.Options;

namespace EchoPhase.DAL.Scylla
{
    public class ScyllaContext : DbContext
    {
        public ScyllaContext(IOptions<DatabaseOptions> options) : base(options.Value.Scylla)
        {
        }

        protected override void OnModelBuilding(ModelBuilder builder)
        {
            base.OnModelBuilding(builder);
        }
    }
}
