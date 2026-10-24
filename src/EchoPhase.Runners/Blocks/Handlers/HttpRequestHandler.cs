// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EchoPhase.Configuration.Runner;
using EchoPhase.Configuration.Runner.Blocks;
using EchoPhase.Runners.Blocks.Params;
using EchoPhase.Scripting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace EchoPhase.Runners.Blocks.Handlers
{
    /// <summary>A response outside 200-299 with failOnError.</summary>
    public sealed class HttpStatusException(int status, string message) : Exception(message)
    {
        public int Status { get; } = status;
    }

    [BlockType(BlockTypes.HttpRequest,
        Description = "Sends an HTTP request to a public address; output: the response body (or { status, headers, body }).")]
    public class HttpRequestHandler : BlockHandlerBase<HttpRequestParams>
    {
        public const string ClientName = "EchoPhase.Blocks.Http";

        private const int ErrorBodyChars = 500;

        private readonly IHttpClientFactory _clients;
        private readonly BlocksHttpOptions _options;

        public HttpRequestHandler(IHttpClientFactory clients, IOptions<RunnerOptions> options)
        {
            _clients = clients;
            _options = options.Value.Blocks.Http;
        }

        public override async Task<BlockResult> ExecuteAsync(BlockCall call, HttpRequestParams param)
        {
            var scope = call.Scope;
            using var request = new HttpRequestMessage(new HttpMethod(param.Method.ToUpperInvariant()), Url(param, scope));

            var headers = param.Headers.ToDictionary(h => h.Key, h => Eval.TypedProcess<string>(h.Value, scope), StringComparer.OrdinalIgnoreCase);
            if (param.Body.ValueKind != JsonValueKind.Undefined)
                request.Content = Content(param, BlockValues.Evaluate(param.Body, scope), headers.GetValueOrDefault("Content-Type"));

            foreach (var (name, value) in headers)
                if (!name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)
                    && !request.Headers.TryAddWithoutValidation(name, value)
                    && request.Content?.Headers.TryAddWithoutValidation(name, value) != true)
                    throw new InvalidOperationException($"Header '{name}' cannot be set.");

            var limit = param.TimeoutSeconds is { } seconds && seconds < _options.Timeout.TotalSeconds
                ? TimeSpan.FromSeconds(seconds)
                : _options.Timeout;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(call.Context.CancellationToken);
            timeout.CancelAfter(limit);

            try
            {
                using var response = await _clients.CreateClient(ClientName)
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

                var bytes = await ReadAsync(response.Content, timeout.Token);
                var status = (int)response.StatusCode;
                if (param.FailOnError && !response.IsSuccessStatusCode)
                {
                    var text = Encoding.UTF8.GetString(bytes);
                    throw new HttpStatusException(status, $"{request.Method} {request.RequestUri} returned {status} {response.ReasonPhrase}: "
                        + (text.Length > ErrorBodyChars ? text[..ErrorBodyChars] + "…" : text));
                }

                var body = Body(param.ResponseType, response.Content.Headers.ContentType, bytes);
                return BlockResult.Main(param.FullResponse
                    ? new Dictionary<string, object?>
                    {
                        ["status"] = status,
                        ["headers"] = response.Headers.Concat(response.Content.Headers)
                            .ToDictionary(h => h.Key.ToLowerInvariant(), h => (object?)string.Join(", ", h.Value)),
                        ["body"] = body
                    }
                    : body);
            }
            catch (OperationCanceledException) when (!call.Context.CancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"{request.Method} {request.RequestUri} took longer than {limit.TotalSeconds:0} s.");
            }
        }

        private static Uri Url(HttpRequestParams param, IDictionary<string, object> scope)
        {
            var url = Eval.TypedProcess<string>(param.Url, scope).Trim();
            if (param.Query.Count > 0)
                url = QueryHelpers.AddQueryString(url,
                    param.Query.Select(q => KeyValuePair.Create(q.Key, (string?)Eval.TypedProcess<string>(q.Value, scope))));

            return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
                ? uri
                : throw new InvalidOperationException($"'{url}' is not an http or https URL.");
        }

        private HttpContent Content(HttpRequestParams param, object? body, string? contentType)
        {
            HttpContent content = param.BodyType switch
            {
                HttpBodyType.Form => new FormUrlEncodedContent(Eval.Plain(body) is IDictionary<string, object?> form
                    ? form.Select(p => KeyValuePair.Create(p.Key, Eval.Format(p.Value)))
                    : throw new InvalidOperationException("A form body must be an object.")),
                HttpBodyType.Text => new StringContent(Eval.Format(body), Encoding.UTF8, "text/plain"),
                HttpBodyType.Json => Json(body),
                _ => body is string text ? new StringContent(text, Encoding.UTF8, "text/plain") : Json(body)
            };

            if (contentType is not null)
                content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            if (content.Headers.ContentLength > _options.MaxRequestBytes)
                throw new InvalidOperationException($"The request body is over {_options.MaxRequestBytes} bytes.");
            return content;
        }

        private static StringContent Json(object? body) => new(Eval.ToJson(body), Encoding.UTF8, "application/json");

        private async Task<byte[]> ReadAsync(HttpContent content, CancellationToken ct)
        {
            if (content.Headers.ContentLength > _options.MaxResponseBytes)
                throw new InvalidOperationException($"The response is over {_options.MaxResponseBytes} bytes.");

            await using var stream = await content.ReadAsStreamAsync(ct);
            using var buffer = new MemoryStream();
            var chunk = new byte[16 * 1024];
            int read;
            while ((read = await stream.ReadAsync(chunk, ct)) > 0)
            {
                if (buffer.Length + read > _options.MaxResponseBytes)
                    throw new InvalidOperationException($"The response is over {_options.MaxResponseBytes} bytes.");
                buffer.Write(chunk, 0, read);
            }
            return buffer.ToArray();
        }

        private static object? Body(HttpResponseType type, MediaTypeHeaderValue? contentType, byte[] bytes)
        {
            if (bytes.Length == 0)
                return null;

            bool json = type == HttpResponseType.Json
                        || (type == HttpResponseType.Auto && contentType?.MediaType is { } media
                            && (media.EndsWith("/json", StringComparison.OrdinalIgnoreCase) || media.EndsWith("+json", StringComparison.OrdinalIgnoreCase)));
            if (!json)
                return Encoding.UTF8.GetString(bytes);

            try
            {
                // Read lazily: a program taking a few fields of a large response
                // does not pay for the rest. The clone outlives the pooled document.
                using var document = JsonDocument.Parse(bytes);
                return Eval.FromJsonLazily(document.RootElement.Clone());
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"The response is not valid JSON: {ex.Message}");
            }
        }
    }
}
