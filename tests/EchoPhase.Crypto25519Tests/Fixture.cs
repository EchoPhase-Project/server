using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EchoPhase.Security.Cryptography.Extensions;
using EchoPhase.Configuration.Extensions;

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
            services.AddSingleton<IConfiguration>(Configuration);
            services.AddConfigurations();
            services.AddCryptography();
            Provider = services.BuildServiceProvider();
        }
    }
}
