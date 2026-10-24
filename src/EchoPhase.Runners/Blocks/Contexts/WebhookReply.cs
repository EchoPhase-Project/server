// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace EchoPhase.Runners.Blocks.Contexts
{
    /// <summary>When a WebhookTrigger answers its caller.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum WebhookRespondMode
    {
        /// <summary>At once: 202 with the run id; the run goes on in the background.</summary>
        Immediately,

        /// <summary>When the run ends: the last block's output (500 if the run failed).</summary>
        OnFinish,

        /// <summary>From a RespondToWebhook block; if none runs, like OnFinish but with { runId, status }.</summary>
        ByBlock
    }

    /// <summary>What the webhook caller gets.</summary>
    public sealed record WebhookResponse(int Status, object? Body, IReadOnlyDictionary<string, string> Headers)
    {
        /// <summary>
        /// The answer is served from the app's own origin, so a program may set
        /// only harmless headers: no cookies, no CSP or CORS changes, no redirects.
        /// </summary>
        public static bool IsAllowedHeader(string name)
            => HeaderName.IsMatch(name)
               && (name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("Cache-Control", StringComparison.OrdinalIgnoreCase)
                   || name.StartsWith("X-", StringComparison.OrdinalIgnoreCase));

        private static readonly Regex HeaderName = new("^[A-Za-z0-9-]{1,64}$", RegexOptions.Compiled);
    }

    /// <summary>
    /// The link between a run and the webhook request waiting for it. The
    /// first response wins; later ones are ignored, like in n8n.
    /// </summary>
    public sealed class WebhookReply(WebhookRespondMode mode)
    {
        private readonly TaskCompletionSource<WebhookResponse> _response = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public WebhookRespondMode Mode { get; } = mode;

        public Task<WebhookResponse> Response => _response.Task;

        public bool HasResponded => _response.Task.IsCompleted;

        public bool TrySend(WebhookResponse response) => _response.TrySetResult(response);
    }
}
