namespace EchoPhase.Services.Models
{
    public class ScheduledTask
    {
        public Guid Id
        {
            get; init;
        }
        public DateTimeOffset ExecuteAt
        {
            get; set;
        }
        public int RetryCount
        {
            get; set;
        }
        public object Params { get; set; } = default!;
        public Func<IServiceProvider, object, CancellationToken, Task> TaskFunc { get; init; } = default!;
        public TimeSpan? RetryDelay { get; set; }
        public TimeSpan? Interval
        {
            get; set;
        }
    }
}
