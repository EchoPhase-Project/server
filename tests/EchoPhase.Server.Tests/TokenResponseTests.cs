// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using EchoPhase.Server.Controllers.Api.v1.Auth;

namespace EchoPhase.Server.Tests
{
    /// <summary>
    /// The token endpoint speaks OAuth (RFC 6749 §5.1, §5.2): its field names are
    /// OAuth's, not the API's camelCase, and an error without a description sends
    /// no <c>error_description</c> at all. The contract check sees a renamed field,
    /// not a null one that should be absent.
    /// </summary>
    public class TokenResponseTests
    {
        // MVC's defaults: camelCase, which the OAuth names must override.
        private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

        [Fact]
        public void Token_HasTheOAuthFieldNames()
            => Assert.Equal(
                """{"access_token":"t","token_type":"Bearer","expires_in":300}""",
                JsonSerializer.Serialize(new ClientTokenResponse("t", "Bearer", 300), Web));

        [Theory]
        [InlineData(null, """{"error":"invalid_client"}""")]
        [InlineData("Use one.", """{"error":"invalid_client","error_description":"Use one."}""")]
        public void Error_SendsADescriptionOnlyWhenThereIsOne(string? description, string json)
            => Assert.Equal(json, JsonSerializer.Serialize(new OAuthErrorResponse("invalid_client", description), Web));
    }
}
