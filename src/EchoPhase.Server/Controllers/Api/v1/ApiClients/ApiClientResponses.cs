// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;

namespace EchoPhase.Server.Controllers.Api.v1.ApiClients
{
    /// <summary>An API client. Its secret is stored only as a hash and never leaves the server.</summary>
    /// <param name="Id">The public client id, for <c>POST /api/v1/auth/token</c>.</param>
    /// <param name="Name">The owner's name for it.</param>
    /// <param name="Permissions">What the client may do, by resource.</param>
    /// <param name="ExpiresAt">When its tokens stop being issued; none: never.</param>
    /// <param name="LastUsedAt">The last time it got a token.</param>
    /// <param name="SecretRotatedAt">The last secret rotation; the previous secret works until revoked.</param>
    /// <param name="CreatedAt">When it was made.</param>
    /// <param name="UpdatedAt">The last change.</param>
    public sealed record ApiClientResponse(
        Guid Id, string Name, IReadOnlyList<ApiClientPermissionResponse> Permissions,
        DateTime? ExpiresAt, DateTime? LastUsedAt, DateTime? SecretRotatedAt, DateTime CreatedAt, DateTime UpdatedAt)
    {
        public static ApiClientResponse From(ApiClient client) => new(
            client.Id, client.Name, client.Permissions.Select(ApiClientPermissionResponse.From).ToList(),
            client.ExpiresAt, client.LastUsedAt, client.SecretRotatedAt, client.CreatedAt, client.UpdatedAt);
    }

    /// <summary>The permissions of an API client on one resource.</summary>
    /// <param name="Resource">The resource, as in <c>resource:action</c>.</param>
    /// <param name="Permissions">The actions on it.</param>
    public sealed record ApiClientPermissionResponse(string Resource, IReadOnlyList<string> Permissions)
    {
        public static ApiClientPermissionResponse From(ApiClientPermissionEntry entry) => new(entry.Resource, entry.Permissions);
    }

    /// <summary>A new API client and its secret, shown this once.</summary>
    /// <param name="Client">The client.</param>
    /// <param name="ClientSecret">The secret; it cannot be read again.</param>
    public sealed record CreatedApiClientResponse(ApiClientResponse Client, string ClientSecret);

    /// <summary>A new secret of an API client, shown this once.</summary>
    /// <param name="ClientSecret">The secret; the previous one works until it is revoked.</param>
    public sealed record ApiClientSecretResponse(string ClientSecret);
}
