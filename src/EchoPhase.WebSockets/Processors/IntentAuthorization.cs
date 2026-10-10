// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections;
using EchoPhase.Security.BitMasks;
using EchoPhase.Security.BitMasks.Constants;
using EchoPhase.Security.BitMasks.Extensions;
using EchoPhase.Types.BitMask;
using EchoPhase.Types.Result.Extensions;
using EchoPhase.WebSockets.Constants;
using EchoPhase.WebSockets.Processors.Payloads;

namespace EchoPhase.WebSockets.Processors
{
    /// <summary>
    /// Sets a connection's intents from a client's request, a list of names:
    /// checked against the permissions of the token the socket was opened
    /// with, then applied as a whole. Shared by Handshake and Adjust.
    /// </summary>
    public sealed class IntentAuthorization
    {
        private readonly IntentsBitMask _intents;
        private readonly ResourcePermissionsBitMask _permissions;

        public IntentAuthorization(IntentsBitMask intents, ResourcePermissionsBitMask permissions)
        {
            _intents = intents;
            _permissions = permissions;
        }

        /// <summary>
        /// Sets the intents named in <paramref name="requested"/> (any case;
        /// empty: none). Null when applied; otherwise the error to send:
        /// unknown names, or names the token's permissions do not allow.
        /// </summary>
        public EventMessage<ErrorPayload>? TryApply(WebSocketConnection connection, IReadOnlyCollection<string> requested)
        {
            var known = Intents.AsEnumerable().ToDictionary(i => i, StringComparer.OrdinalIgnoreCase);
            var names = new HashSet<string>(StringComparer.Ordinal);
            var unknown = new List<string>();
            foreach (var name in requested)
            {
                if (known.TryGetValue(name, out var canonical))
                    names.Add(canonical);
                else
                    unknown.Add(name);
            }

            if (unknown.Count > 0)
                return ErrorMessages.Create(ErrorCodes.InvalidPayload,
                    $"Unknown intents: {Listed(unknown)}. The known ones: GET /api/v1/gateway/intents.");

            var disallowed = IntentRequirements.Disallowed(names, _permissions.GetPermissions(connection.User));
            if (disallowed.Count > 0)
                return ErrorMessages.Create(ErrorCodes.DisallowedIntents,
                    $"Not allowed by the token's permissions: {string.Join(", ", disallowed)}.");

            // Stored as a bit mask: checking an event's intents per connection stays cheap.
            connection.Intents = names.Count == 0 ? BitMaskBase.Empty : _intents.Add(BitMaskBase.Empty, [.. names]);
            return null;
        }

        // What the client sent, echoed back bounded: at most a few names.
        private static string Listed(IReadOnlyList<string> names)
        {
            const int Shown = 10;
            var listed = string.Join(", ", names.Take(Shown));
            return names.Count > Shown ? $"{listed} and {names.Count - Shown} more" : listed;
        }

        public string[] Names(BitArray intents)
            => _intents.Decode(intents).TryGetValue(out var names) ? names : [];
    }
}
