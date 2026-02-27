using EchoPhase.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Crypto25519Tests
{
    public class Fixture
    {
        public ServiceProvider Provider
        {
            get;
        }
        public IConfiguration Configuration
        {
            get;
        }

        public Fixture()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            Configuration = builder.Build();

            var services = new ServiceCollection();
            services.AddCrypto25519(Configuration.GetSection("Crypto25519"));
            Provider = services.BuildServiceProvider();
        }
    }
}
