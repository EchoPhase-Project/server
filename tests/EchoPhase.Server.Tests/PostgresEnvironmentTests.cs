// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Configuration.Database.Postgres;
using Npgsql;

namespace EchoPhase.Server.Tests
{
    public class PostgresEnvironmentTests
    {
        private static readonly Dictionary<string, string> Required = new()
        {
            ["POSTGRES_HOST"] = "db.example.internal",
            ["POSTGRES_PORT"] = "5433",
            ["POSTGRES_USER"] = "echophase",
            ["POSTGRES_PASSWORD"] = "secret",
            ["POSTGRES_DB"] = "echophase",
        };

        private static NpgsqlConnectionStringBuilder Parse(Dictionary<string, string> variables)
        {
            var connectionString = PostgresEnvironment.ConnectionString(name => variables.GetValueOrDefault(name));
            Assert.NotNull(connectionString);
            return new NpgsqlConnectionStringBuilder(connectionString);
        }

        private static Dictionary<string, string> With(params (string Name, string Value)[] extra)
        {
            var variables = new Dictionary<string, string>(Required);
            foreach (var (name, value) in extra)
                variables[name] = value;
            return variables;
        }

        [Fact]
        public void ConnectionString_FromTheVariables_ReadsBackInNpgsql()
        {
            var builder = Parse(Required);

            Assert.Equal("db.example.internal", builder.Host);
            Assert.Equal(5433, builder.Port);
            Assert.Equal("echophase", builder.Database);
            Assert.Equal("echophase", builder.Username);
            Assert.Equal("secret", builder.Password);
            Assert.Equal(GssEncryptionMode.Disable, builder.GssEncryptionMode);
            Assert.Equal(SslMode.Prefer, builder.SslMode);
        }

        [Theory]
        [InlineData("POSTGRES_HOST")]
        [InlineData("POSTGRES_PORT")]
        [InlineData("POSTGRES_USER")]
        [InlineData("POSTGRES_PASSWORD")]
        [InlineData("POSTGRES_DB")]
        public void ConnectionString_WithoutARequiredVariable_IsNull(string missing)
        {
            var variables = new Dictionary<string, string>(Required);
            variables.Remove(missing);

            Assert.Null(PostgresEnvironment.ConnectionString(name => variables.GetValueOrDefault(name)));
        }

        [Fact]
        public void ConnectionString_PasswordWithSeparators_IsQuoted()
        {
            const string Password = "a;b='c\"d e";

            var builder = Parse(With(("POSTGRES_PASSWORD", Password)));

            Assert.Equal(Password, builder.Password);
            Assert.Equal("db.example.internal", builder.Host);
        }

        [Theory]
        [InlineData("VerifyFull", SslMode.VerifyFull)]
        [InlineData("verify-full", null)]
        [InlineData("require", SslMode.Require)]
        [InlineData(" Disable ", SslMode.Disable)]
        public void ConnectionString_SslMode_IsSetCaseInsensitively(string value, SslMode? expected)
        {
            var variables = With(("POSTGRES_SSL_MODE", value));

            if (expected is null)
            {
                var error = Assert.Throws<InvalidOperationException>(() =>
                    PostgresEnvironment.ConnectionString(name => variables.GetValueOrDefault(name)));
                Assert.Contains("POSTGRES_SSL_MODE", error.Message);
                return;
            }

            Assert.Equal(expected, Parse(variables).SslMode);
        }

        [Fact]
        public void ConnectionString_RootCertificate_IsPassedOn()
        {
            var builder = Parse(With(
                ("POSTGRES_SSL_MODE", "VerifyFull"),
                ("POSTGRES_SSL_ROOT_CERT", "/etc/echophase/postgres-ca/ca.crt")));

            Assert.Equal(SslMode.VerifyFull, builder.SslMode);
            Assert.Equal("/etc/echophase/postgres-ca/ca.crt", builder.RootCertificate);
        }
    }
}
