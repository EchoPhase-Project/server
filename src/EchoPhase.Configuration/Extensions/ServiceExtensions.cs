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
                    // GSS (Kerberos) encryption is unused; Npgsql's default (Prefer)
                    // tries to load libgssapi_krb5, absent from the aspnet image,
                    // and logs an error on every start.
                    options.Postgres.ConnectionString =
                        $"Host={pgHost};Port={pgPort};Database={pgDb};Username={pgUser};Password={pgPw};GSS Encryption Mode=Disable";
                }

                // Scylla env override
                var scyllaContact = Environment.GetEnvironmentVariable("SCYLLA_CONTACT_POINT");
                var scyllaKeyspace = Environment.GetEnvironmentVariable("SCYLLA_KEYSPACE");
                var scyllaUser = Environment.GetEnvironmentVariable("SCYLLA_USERNAME");
                var scyllaPw = Environment.GetEnvironmentVariable("SCYLLA_PASSWORD");
                var scyllaLocalDc = Environment.GetEnvironmentVariable("SCYLLA_LOCAL_DC");
                var scyllaAutoCreate = Environment.GetEnvironmentVariable("SCYLLA_AUTO_CREATE_KEYSPACE");

                if (!string.IsNullOrWhiteSpace(scyllaContact))
                    options.Scylla.ContactPoint = scyllaContact;
                if (!string.IsNullOrWhiteSpace(scyllaKeyspace))
                    options.Scylla.Keyspace = scyllaKeyspace;
                if (!string.IsNullOrWhiteSpace(scyllaUser))
                    options.Scylla.Username = scyllaUser;
                if (!string.IsNullOrWhiteSpace(scyllaPw))
                    options.Scylla.Password = scyllaPw;
                if (!string.IsNullOrWhiteSpace(scyllaLocalDc))
                    options.Scylla.LocalDatacenter = scyllaLocalDc;
                if (bool.TryParse(scyllaAutoCreate, out var autoCreate))
                    options.Scylla.AutoCreateKeyspace = autoCreate;
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

            services.AddOptions<Configuration.Network.ForwardedHeadersSettings>()
                .BindConfiguration(Configuration.Network.ForwardedHeadersSettings.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<IValidateOptions<Configuration.Network.ForwardedHeadersSettings>, Configuration.Network.ForwardedHeadersValidator>();

            services.AddOptions<Configuration.RateLimiting.RateLimitingSettings>()
                .BindConfiguration(Configuration.RateLimiting.RateLimitingSettings.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<IValidateOptions<Configuration.RateLimiting.RateLimitingSettings>, Configuration.RateLimiting.RateLimitingValidator>();

            services.AddOptions<Configuration.Clients.ClientsOptions>()
                .BindConfiguration(Configuration.Clients.ClientsOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<IValidateOptions<Configuration.Clients.ClientsOptions>, Configuration.Clients.ClientsValidator>();

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
