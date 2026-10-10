// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Configuration.WebSocket
{
    public class WebSocketOptions : IValidatable
    {
        private const int MinHeartbeatSeconds = 10;
        private const int MaxHeartbeatSeconds = 300;
        private const int MinCloseTimeoutSeconds = 1;
        private const int MaxCloseTimeoutSeconds = 60;
        private const int MinRevalidationSeconds = 5;
        private const int MaxRevalidationSeconds = 600;

        public const string SectionName = "WebSocket";

        /// <summary>
        /// Heartbeat interval. Format: "hh:mm:ss" or seconds. Default: 45 seconds.
        /// </summary>
        public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(45);

        /// <summary>
        /// WebSocket close timeout. Format: "hh:mm:ss" or seconds. Default: 5 seconds.
        /// </summary>
        public TimeSpan CloseTimeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// How often open realtime connections (gateway and event hub) are
        /// re-checked; a revoked or expired token is disconnected within this
        /// window. Default: 30 seconds.
        /// </summary>
        public TimeSpan RevalidationInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Largest message a client may send (all frames together); a bigger
        /// one closes the connection with MessageTooBig. Default: 64 KiB.
        /// </summary>
        public int MaxMessageBytes { get; set; } = 64 * 1024;

        /// <summary>
        /// Browser origins allowed to open the gateway besides the server's own
        /// host, e.g. "http://localhost:5173" for the Vite dev server. Clients
        /// that send no Origin (bots, CLIs) are not affected.
        /// </summary>
        public string[] AllowedOrigins { get; set; } = [];

        public IValidationResult Validate()
        {
            if (HeartbeatInterval.TotalSeconds < MinHeartbeatSeconds)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(HeartbeatInterval),
                        $"HeartbeatInterval must be at least {MinHeartbeatSeconds} seconds."));

            if (HeartbeatInterval.TotalSeconds > MaxHeartbeatSeconds)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(HeartbeatInterval),
                        $"HeartbeatInterval must be at most {MaxHeartbeatSeconds} seconds."));

            if (CloseTimeout.TotalSeconds < MinCloseTimeoutSeconds)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(CloseTimeout),
                        $"CloseTimeout must be at least {MinCloseTimeoutSeconds} second."));

            if (CloseTimeout.TotalSeconds > MaxCloseTimeoutSeconds)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(CloseTimeout),
                        $"CloseTimeout must be at most {MaxCloseTimeoutSeconds} seconds."));

            if (RevalidationInterval.TotalSeconds < MinRevalidationSeconds
                || RevalidationInterval.TotalSeconds > MaxRevalidationSeconds)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(RevalidationInterval),
                        $"RevalidationInterval must be between {MinRevalidationSeconds} and {MaxRevalidationSeconds} seconds."));

            if (MaxMessageBytes is < 1024 or > 16 * 1024 * 1024)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(MaxMessageBytes), "MaxMessageBytes must be between 1 KiB and 16 MiB."));

            return ValidationResult.Success();
        }
    }
}
