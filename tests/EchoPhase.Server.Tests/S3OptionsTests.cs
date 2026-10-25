// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Configuration.Storage.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EchoPhase.Server.Tests
{
    public class S3OptionsTests
    {
        private static S3Options Valid() => new()
        {
            Endpoint = "http://seaweedfs:8333",
            Bucket = "echophase",
            AccessKeyId = "key",
            SecretAccessKey = "secret",
        };

        [Fact]
        public void Validate_WithoutAnEndpoint_IsNotConfiguredAndValid()
        {
            var options = new S3Options();

            Assert.False(options.IsConfigured);
            Assert.True(options.Validate().Successful);
        }

        [Fact]
        public void Validate_ACompleteConfiguration_IsValid()
        {
            var options = Valid();

            Assert.True(options.IsConfigured);
            Assert.True(options.Validate().Successful);
        }

        [Theory]
        [InlineData("seaweedfs:8333")]
        [InlineData("ftp://s3.example.com")]
        [InlineData("not a url")]
        public void Validate_AnEndpointThatIsNotAnHttpUrl_Fails(string endpoint)
        {
            var options = Valid();
            options.Endpoint = endpoint;

            Assert.False(options.Validate().Successful);
        }

        [Theory]
        [InlineData("")]
        [InlineData("ab")]
        [InlineData("EchoPhase")]
        [InlineData("-echophase")]
        [InlineData("echophase_files")]
        public void Validate_AnInvalidBucketName_Fails(string bucket)
        {
            var options = Valid();
            options.Bucket = bucket;

            Assert.False(options.Validate().Successful);
        }

        [Theory]
        [InlineData("key", "")]
        [InlineData("", "secret")]
        public void Validate_OneKeyWithoutTheOther_Fails(string accessKeyId, string secretAccessKey)
        {
            var options = Valid();
            options.AccessKeyId = accessKeyId;
            options.SecretAccessKey = secretAccessKey;

            Assert.False(options.Validate().Successful);
        }

        [Fact]
        public void Apply_SetVariables_WinAndUnsetOnesKeepTheValues()
        {
            var options = new S3Options { Region = "eu-west-1", Bucket = "from-config" };
            var variables = new Dictionary<string, string>
            {
                ["S3_ENDPOINT"] = " https://s3.example.com ",
                ["S3_BUCKET"] = "echophase",
                ["S3_ACCESS_KEY_ID"] = "key",
                ["S3_SECRET_ACCESS_KEY"] = "secret",
                ["S3_FORCE_PATH_STYLE"] = "false",
                ["S3_REGION"] = "",
            };

            S3Environment.Apply(options, name => variables.GetValueOrDefault(name));

            Assert.Equal("https://s3.example.com", options.Endpoint);
            Assert.Equal("echophase", options.Bucket);
            Assert.Equal("eu-west-1", options.Region);
            Assert.Equal("key", options.AccessKeyId);
            Assert.Equal("secret", options.SecretAccessKey);
            Assert.False(options.ForcePathStyle);
        }

        [Fact]
        public void Apply_AnUnreadablePathStyle_KeepsTheDefault()
        {
            var options = new S3Options();

            S3Environment.Apply(options, name => name == "S3_FORCE_PATH_STYLE" ? "maybe" : null);

            Assert.True(options.ForcePathStyle);
        }

        [Fact]
        public void Options_FromTheConfigurationSection_AreBoundAndValidated()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Storage:S3:Endpoint"] = "http://seaweedfs:8333",
                    ["Storage:S3:Bucket"] = "Not_A_Bucket",
                })
                .Build();
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddOptions<S3Options>().BindConfiguration(S3Options.SectionName);
            services.AddSingleton<IValidateOptions<S3Options>, S3Validator>();

            var error = Assert.Throws<OptionsValidationException>(() =>
                services.BuildServiceProvider().GetRequiredService<IOptions<S3Options>>().Value);
            Assert.Contains("Bucket", error.Message);
        }
    }
}
