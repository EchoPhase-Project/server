// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Security.Claims;
using System.Text.Json;
using EchoPhase.Security.Authorization;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Types.Claims;

namespace EchoPhase.Middlewares
{
    /// <summary>
    /// Tags every log entry of an authenticated request with who is calling:
    /// the user, the API client, or the actor behind an impersonation. Requests
    /// made with an impersonation token are additionally logged one by one.
    /// </summary>
    public class PrincipalLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<PrincipalLoggingMiddleware> _logger;

        public PrincipalLoggingMiddleware(RequestDelegate next, ILogger<PrincipalLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var user = context.User;
            var userId = user.FindFirst("sub")?.Value;

            if (userId is null)
            {
                await _next(context);
                return;
            }

            var kind = PrincipalKinds.Of(user);
            var clientId = user.FindFirst(EchoPhaseClaimTypes.ClientId)?.Value;
            var impersonationId = user.FindFirst(EchoPhaseClaimTypes.ImpersonationId)?.Value;
            var actorId = kind == PrincipalKind.Impersonation ? ReadActorId(user) : null;

            var principal = Describe(kind, userId, clientId, actorId, impersonationId);

            using (_logger.BeginScope(new PrincipalScope(principal,
            [
                new("Principal", principal),
                new("PrincipalKind", kind.ToString()),
                new("UserId", userId),
                new("ClientId", clientId),
                new("ActorId", actorId),
                new("ImpersonationId", impersonationId)
            ])))
            {
                if (kind == PrincipalKind.Impersonation)
                    _logger.LogInformation(
                        "Impersonated request {Method} {Path} by {ActorId} as {UserId} ({ImpersonationId})",
                        context.Request.Method, context.Request.Path, actorId, userId, impersonationId);

                await _next(context);
            }
        }

        private static string Describe(
            PrincipalKind kind, string userId, string? clientId, string? actorId, string? impersonationId)
            => kind switch
            {
                PrincipalKind.ApiClient => $"client:{clientId} user:{userId}",
                PrincipalKind.Impersonation => $"actor:{actorId} as user:{userId} imp:{impersonationId}",
                _ => $"user:{userId}"
            };

        private static string? ReadActorId(ClaimsPrincipal user)
        {
            var act = user.FindFirst(EchoPhaseClaimTypes.Act)?.Value;
            if (act is null)
                return null;

            try
            {
                using var doc = JsonDocument.Parse(act);
                return doc.RootElement.TryGetProperty("sub", out var sub) ? sub.GetString() : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Enumerates as fields for structured sinks (Serilog file), and prints as
    /// the short principal string in text formatters, which call ToString()
    /// on scopes (a plain dictionary would print its type name).
    /// </summary>
    internal sealed class PrincipalScope : IReadOnlyList<KeyValuePair<string, object?>>
    {
        private readonly string _text;
        private readonly IReadOnlyList<KeyValuePair<string, object?>> _fields;

        public PrincipalScope(string text, IReadOnlyList<KeyValuePair<string, object?>> fields)
        {
            _text = text;
            _fields = fields;
        }

        public KeyValuePair<string, object?> this[int index] => _fields[index];

        public int Count => _fields.Count;

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _fields.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString() => _text;
    }

    public static class PrincipalLoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UsePrincipalLogging(this IApplicationBuilder builder)
            => builder.UseMiddleware<PrincipalLoggingMiddleware>();
    }
}
