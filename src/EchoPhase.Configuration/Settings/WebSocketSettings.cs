namespace EchoPhase.Configuration.Settings
{
    public class WebSocketSettings : IValidatable
    {
        private const int MinHeartbeatSeconds = 10;
        private const int MaxHeartbeatSeconds = 300;
        private const int MinCloseTimeoutSeconds = 1;
        private const int MaxCloseTimeoutSeconds = 60;

        /// <summary>
        /// Heartbeat interval. Format: "hh:mm:ss" or seconds. Default: 45 seconds.
        /// </summary>
        public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(45);

        /// <summary>
        /// WebSocket close timeout. Format: "hh:mm:ss" or seconds. Default: 5 seconds.
        /// </summary>
        public TimeSpan CloseTimeout { get; set; } = TimeSpan.FromSeconds(5);

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

            return ValidationResult.Success();
        }
    }
}
