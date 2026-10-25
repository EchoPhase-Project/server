// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net;
using System.Net.Http.Json;
using EchoPhase.Clients.Abstractions;
using EchoPhase.Connections.Models;
using EchoPhase.Connections.Providers;
using EchoPhase.Connections.Services;
using EchoPhase.Types.Result;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Connections.Http
{
    /// <summary>
    /// Authenticates requests that carry a <see cref="ConnectionRef"/>: applies
    /// the stored credentials, refreshes them proactively, and on a 401 refreshes
    /// and retries once. Must sit outside any retry policy.
    /// </summary>
    /// <remarks>
    /// Handlers outlive requests (they are pooled by IHttpClientFactory), so
    /// scoped services such as the DbContext are resolved per call.
    /// </remarks>
    public class ConnectionAuthHandler : DelegatingHandler
    {
        private readonly string _provider;
        private readonly IServiceScopeFactory _scopes;

        public ConnectionAuthHandler(string provider, IServiceScopeFactory scopes)
        {
            _provider = provider;
            _scopes = scopes;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            if (!request.Options.TryGetValue(ConnectionRef.OptionKey, out var reference))
                return await base.SendAsync(request, ct);

            ResolvedCredential credential;
            await using (var scope = _scopes.CreateAsyncScope())
            {
                var resolved = await scope.ServiceProvider
                    .GetRequiredService<IConnectionService>()
                    .ResolveAsync(reference, _provider, ct);

                if (!resolved.Successful)
                    return ErrorResponse(request, resolved.Error);

                credential = resolved.Value;
                Provider(scope).Apply(request, credential);
            }

            // Buffer the body so it can be sent a second time.
            if (request.Content is not null)
                await request.Content.LoadIntoBufferAsync(ct);

            var response = await base.SendAsync(request, ct);
            if (response.StatusCode != HttpStatusCode.Unauthorized)
                return response;

            await using (var scope = _scopes.CreateAsyncScope())
            {
                var recovered = await scope.ServiceProvider
                    .GetRequiredService<IConnectionService>()
                    .RecoverAsync(reference, _provider, credential.AccessToken, ct);

                if (!recovered.Successful)
                {
                    // Not a token problem (e.g. missing scope) or a transient
                    // failure: let the caller see the provider's own answer.
                    if (recovered.Error.Code != ConnectionErrors.ReauthRequired)
                        return response;

                    response.Dispose();
                    return ErrorResponse(request, recovered.Error);
                }

                var retry = await CloneAsync(request, ct);
                Provider(scope).Apply(retry, recovered.Value);

                response.Dispose();
                return await base.SendAsync(retry, ct);
            }
        }

        private IConnectionProvider Provider(AsyncServiceScope scope)
            => scope.ServiceProvider.GetRequiredService<IConnectionProviderRegistry>().Find(_provider)
               ?? throw new InvalidOperationException($"No connection provider registered for '{_provider}'.");

        private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Version = request.Version,
                VersionPolicy = request.VersionPolicy
            };

            foreach (var header in request.Headers)
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

            foreach (var option in request.Options)
                ((IDictionary<string, object?>)clone.Options)[option.Key] = option.Value;

            if (request.Content is not null)
            {
                clone.Content = new ByteArrayContent(await request.Content.ReadAsByteArrayAsync(ct));
                foreach (var header in request.Content.Headers)
                    clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return clone;
        }

        /// <summary>
        /// A provider-shaped error response, so client error parsing still works:
        /// Discord reads <c>code</c>, Twitch reads <c>status</c>, both read <c>message</c>.
        /// </summary>
        private static HttpResponseMessage ErrorResponse(HttpRequestMessage request, IServiceError error)
        {
            var status = error.Code switch
            {
                ConnectionErrors.NotFound => HttpStatusCode.NotFound,
                ConnectionErrors.ReauthRequired => HttpStatusCode.Unauthorized,
                ConnectionErrors.NotConfigured => HttpStatusCode.ServiceUnavailable,
                ConnectionErrors.Upstream => HttpStatusCode.BadGateway,
                _ => HttpStatusCode.BadRequest
            };

            return new HttpResponseMessage(status)
            {
                RequestMessage = request,
                Content = JsonContent.Create(new
                {
                    status = (int)status,
                    code = (int)status,
                    error = error.Code,
                    message = error.Message
                })
            };
        }
    }
}
