using EchoPhase.DAL.Postgres.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Security.Hashers.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddPasswordHasher(this IServiceCollection services)
        {
            services.AddSingleton<IPasswordHasher<User>, Argon2Hasher>();

            return services;
        }
    }
}
