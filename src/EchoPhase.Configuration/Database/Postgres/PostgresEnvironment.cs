// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Data.Common;

namespace EchoPhase.Configuration.Database.Postgres
{
    /// <summary>
    /// The Postgres connection string from the <c>POSTGRES_*</c> environment
    /// variables that containers and the Helm chart set, instead of the one in
    /// the configuration files.
    /// </summary>
    public static class PostgresEnvironment
    {
        /// <summary>
        /// The values <c>POSTGRES_SSL_MODE</c> accepts: Npgsql's SSL modes.
        /// </summary>
        public static readonly IReadOnlyList<string> SslModes =
            ["Disable", "Allow", "Prefer", "Require", "VerifyCA", "VerifyFull"];

        /// <summary>
        /// Builds the connection string, or returns null when one of host,
        /// port, user, password and database is not set (the configuration's
        /// connection string applies then).
        /// </summary>
        /// <param name="variable">Reads an environment variable by name.</param>
        /// <remarks>
        /// <c>POSTGRES_SSL_MODE</c> sets the SSL mode (Npgsql's default,
        /// Prefer, when unset); <c>POSTGRES_SSL_ROOT_CERT</c> the path of the
        /// CA certificate VerifyCA and VerifyFull check the server against
        /// (the system's trusted CAs when unset). Values are quoted as needed,
        /// so a password may contain any character.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// <c>POSTGRES_SSL_MODE</c> is not one of <see cref="SslModes"/>.
        /// </exception>
        public static string? ConnectionString(Func<string, string?> variable)
        {
            var host = variable("POSTGRES_HOST");
            var port = variable("POSTGRES_PORT");
            var user = variable("POSTGRES_USER");
            var password = variable("POSTGRES_PASSWORD");
            var database = variable("POSTGRES_DB");

            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(port) || string.IsNullOrEmpty(user) ||
                string.IsNullOrEmpty(password) || string.IsNullOrEmpty(database))
                return null;

            var builder = new DbConnectionStringBuilder
            {
                ["Host"] = host,
                ["Port"] = port,
                ["Database"] = database,
                ["Username"] = user,
                ["Password"] = password,
                // GSS (Kerberos) encryption is unused; Npgsql's default (Prefer)
                // tries to load libgssapi_krb5, absent from the aspnet image,
                // and logs an error on every start.
                ["GSS Encryption Mode"] = "Disable",
            };

            var sslMode = variable("POSTGRES_SSL_MODE");
            if (!string.IsNullOrWhiteSpace(sslMode))
            {
                builder["SSL Mode"] = SslModes.FirstOrDefault(mode =>
                        string.Equals(mode, sslMode.Trim(), StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException(
                        $"POSTGRES_SSL_MODE must be one of {string.Join(", ", SslModes)}, not '{sslMode}'.");
            }

            var rootCertificate = variable("POSTGRES_SSL_ROOT_CERT");
            if (!string.IsNullOrWhiteSpace(rootCertificate))
                builder["Root Certificate"] = rootCertificate;

            return builder.ConnectionString;
        }
    }
}
