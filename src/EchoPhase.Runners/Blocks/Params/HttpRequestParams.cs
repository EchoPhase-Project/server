// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using System.Text.Json.Serialization;
using EchoPhase.Configuration;
using EchoPhase.Types.Validation;

namespace EchoPhase.Runners.Blocks.Params
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum HttpBodyType
    {
        /// <summary>Text as text/plain (or the Content-Type header), anything else as JSON.</summary>
        Auto,
        Json,
        Text,

        /// <summary>An object as application/x-www-form-urlencoded.</summary>
        Form
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum HttpResponseType
    {
        /// <summary>JSON when the response says so, else text.</summary>
        Auto,
        Json,
        Text
    }

    public class HttpRequestParams : IValidatable
    {
        public static readonly string[] Methods = ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"];

        [JsonPropertyName("method")]
        public string Method { get; set; } = "GET";

        /// <summary>A template: "https://api.example.com/users/@input.id". http or https only.</summary>
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        /// <summary>Templates, added to the URL's query string (encoded).</summary>
        [JsonPropertyName("query")]
        public Dictionary<string, string> Query { get; set; } = [];

        /// <summary>Templates: "Authorization": "Bearer @token".</summary>
        [JsonPropertyName("headers")]
        public Dictionary<string, string> Headers { get; set; } = [];

        /// <summary>A template ("@input") or a JSON value; absent: no body.</summary>
        [JsonPropertyName("body")]
        public JsonElement Body { get; set; }

        [JsonPropertyName("bodyType")]
        public HttpBodyType BodyType { get; set; } = HttpBodyType.Auto;

        [JsonPropertyName("responseType")]
        public HttpResponseType ResponseType { get; set; } = HttpResponseType.Auto;

        /// <summary>Output { status, headers, body } instead of the body alone.</summary>
        [JsonPropertyName("fullResponse")]
        public bool FullResponse { get; set; }

        /// <summary>Fail on a status outside 200-299 (onError and retry then apply); false: any status is an output.</summary>
        [JsonPropertyName("failOnError")]
        public bool FailOnError { get; set; } = true;

        /// <summary>Seconds; the server's limit if absent or larger.</summary>
        [JsonPropertyName("timeoutSeconds")]
        public int? TimeoutSeconds { get; set; }

        public IValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Url))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Url), "Url cannot be empty."));

            if (!Methods.Contains(Method.ToUpperInvariant()))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Method), $"Method must be one of {string.Join(", ", Methods)}."));

            if (TimeoutSeconds is < 1)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(TimeoutSeconds), "TimeoutSeconds must be positive."));

            return ValidationResult.Success();
        }
    }
}
