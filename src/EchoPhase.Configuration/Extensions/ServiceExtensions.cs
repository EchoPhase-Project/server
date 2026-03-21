// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EchoPhase.Configuration.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddConfigurations(this IServiceCollection services)
        {
            services.Configure<Database.DatabaseOptions>(options =>
            {
                // Redis env override
                var valkeyHost = Environment.GetEnvironmentVariable("VALKEY_HOST");
                var valkeyPort = Environment.GetEnvironmentVariable("VALKEY_PORT");
                var valkeyPw = Environment.GetEnvironmentVariable("VALKEY_PASSWORD");
                var valkeySsl = Environment.GetEnvironmentVariable("VALKEY_SSL");
                var valkeyTimeout = Environment.GetEnvironmentVariable("VALKEY_TIMEOUT");

                if (!string.IsNullOrEmpty(valkeyHost) && !string.IsNullOrEmpty(valkeyPort))
                {
                    var parts = new List<string> { $"{valkeyHost}:{valkeyPort}" };

                    if (!string.IsNullOrWhiteSpace(valkeyPw))
                        parts.Add($"password={valkeyPw}");
                    if (bool.TryParse(valkeySsl, out var sslRes))
                        parts.Add($"ssl={sslRes}");
                    if (int.TryParse(valkeyTimeout, out var timeoutRes))
                        parts.Add($"connectTimeout={timeoutRes}");

                    options.Redis.ConnectionString = string.Join(", ", parts);
                }

                // Postgres env override
                var pgHost = Environment.GetEnvironmentVariable("POSTGRES_HOST");
                var pgPort = Environment.GetEnvironmentVariable("POSTGRES_PORT");
                var pgPw = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");
                var pgUser = Environment.GetEnvironmentVariable("POSTGRES_USER");
                var pgDb = Environment.GetEnvironmentVariable("POSTGRES_DB");

                if (!string.IsNullOrEmpty(pgHost) && !string.IsNullOrEmpty(pgPort) &&
                    !string.IsNullOrEmpty(pgPw) && !string.IsNullOrEmpty(pgUser) &&
                    !string.IsNullOrEmpty(pgDb))
                {
                    options.Postgres.ConnectionString =
                        $"Host={pgHost};Port={pgPort};Database={pgDb};Username={pgUser};Password={pgPw}";
                }

                // Scylla env override
                var scyllaContact = Environment.GetEnvironmentVariable("SCYLLA_CONTACT_POINT");
                var scyllaKeyspace = Environment.GetEnvironmentVariable("SCYLLA_KEYSPACE");
                var scyllaUser = Environment.GetEnvironmentVariable("SCYLLA_USERNAME");
                var scyllaPw = Environment.GetEnvironmentVariable("SCYLLA_PASSWORD");

                if (!string.IsNullOrWhiteSpace(scyllaContact))
                    options.Scylla.ContactPoint = scyllaContact;
                if (!string.IsNullOrWhiteSpace(scyllaKeyspace))
                    options.Scylla.Keyspace = scyllaKeyspace;
                if (!string.IsNullOrWhiteSpace(scyllaUser))
                    options.Scylla.Username = scyllaUser;
                if (!string.IsNullOrWhiteSpace(scyllaPw))
                    options.Scylla.Password = scyllaPw;
            });

            services.AddOptions<Database.DatabaseOptions>()
                .BindConfiguration(Database.DatabaseOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<IValidateOptions<Database.DatabaseOptions>, Database.DatabaseValidator>();

            services.AddOptions<Configuration.Authentication.AuthenticationOptions>()
                .BindConfiguration(Configuration.Authentication.AuthenticationOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<IValidateOptions<Configuration.Authentication.AuthenticationOptions>, Configuration.Authentication.AuthenticationValidator>();

            services.AddOptions<Configuration.WebSocket.WebSocketOptions>()
                .BindConfiguration(Configuration.WebSocket.WebSocketOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<IValidateOptions<Configuration.WebSocket.WebSocketOptions>, Configuration.WebSocket.WebSocketValidator>();

            services.AddOptions<Configuration.Cryptography.CryptographyOptions>()
                .BindConfiguration(Configuration.Cryptography.CryptographyOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<IValidateOptions<Configuration.Cryptography.CryptographyOptions>, Configuration.Cryptography.CryptographyValidator>();

            services.AddOptions<Configuration.Argon2.Argon2Options>()
                .BindConfiguration(Configuration.Argon2.Argon2Options.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<IValidateOptions<Configuration.Argon2.Argon2Options>, Configuration.Argon2.Argon2Validator>();

            services.AddOptions<Configuration.Runner.RunnerOptions>()
                .BindConfiguration(Configuration.Runner.RunnerOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<IValidateOptions<Configuration.Runner.RunnerOptions>, Configuration.Runner.RunnerValidator>();

            return services;
        }
    }
}
