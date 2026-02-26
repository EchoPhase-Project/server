using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Scheduling
{
    public class DelayedTaskScheduler : IDisposable
    {
        private readonly IServiceProvider _provider;
        private readonly ConcurrentDictionary<Guid, ScheduledTask> _taskMap = new();
        private readonly PriorityQueue<ScheduledTask, DateTimeOffset> _taskQueue = new();
        private readonly SemaphoreSlim _semaphore = new(0);
        private readonly object _lock = new();
        private readonly CancellationTokenSource _cts = new();

        private bool _isRunning = false;

        public DelayedTaskScheduler(IServiceProvider provider)
        {
            _provider = provider;
        }

        public Guid Enqueue<TParams>(
            TParams taskParams,
            TimeSpan delay,
            Func<IServiceProvider, TParams, CancellationToken, Task> taskFunc,
            int retryCount = 0,
            TimeSpan? retryDelay = null,
            TimeSpan? interval = null)
        {
            var id = Guid.NewGuid();
            if (taskParams is null)
                throw new ArgumentNullException(nameof(taskParams));

            var scheduled = new ScheduledTask
            {
                Id = id,
                Params = taskParams,
                RetryCount = retryCount,
                ExecuteAt = DateTimeOffset.UtcNow.Add(delay),
                Interval = interval,
                RetryDelay = retryDelay,
                TaskFunc = async (sp, obj, ct) =>
                {
                    if (obj is null)
                        throw new ArgumentNullException(nameof(obj));

                    if (obj is not TParams p)
                        throw new InvalidCastException(
                            $"Expected type {typeof(TParams).Name}, instead got {obj.GetType().Name}");

                    using var scope = sp.CreateScope();
                    await taskFunc(scope.ServiceProvider, p, ct);
                }
            };

            _taskMap[id] = scheduled;
            lock (_lock)
            {
                _taskQueue.Enqueue(scheduled, scheduled.ExecuteAt);
                _semaphore.Release();
                if (!_isRunning)
                {
                    _isRunning = true;
                    _ = Task.Run(() => RunLoopAsync(_cts.Token));
                }
            }

            return id;
        }

        public bool Remove(Guid id)
        {
            return _taskMap.TryRemove(id, out _);
        }

        public bool Update<TParams>(Guid id, TParams newParams, TimeSpan? newDelay = null, int? retryCount = null, TimeSpan? retryDelay = null, TimeSpan? interval = null)
        {
            if (!_taskMap.TryGetValue(id, out var task))
                return false;

            if (newParams != null)
                task.Params = newParams;
            if (newDelay.HasValue)
                task.ExecuteAt = DateTimeOffset.UtcNow.Add(newDelay.Value);
            if (retryCount.HasValue)
                task.RetryCount = retryCount.Value;
            if (interval.HasValue)
                task.Interval = interval;
            if (retryDelay.HasValue)
                task.RetryDelay = retryDelay;

            lock (_lock)
            {
                _taskQueue.Enqueue(task, task.ExecuteAt);
                _semaphore.Release();
            }

            return true;
        }

        private ScheduledTask? GetNextTask()
        {
            while (_taskQueue.TryPeek(out var candidate, out var priority))
            {
                if (_taskMap.TryGetValue(candidate.Id, out var actual) && actual.ExecuteAt == priority)
                    return actual;

                _taskQueue.Dequeue();
            }
            return null;
        }

        private async Task RunLoopAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ScheduledTask? taskToRun = null;
                TimeSpan waitTime;

                lock (_lock)
                {
                    var next = GetNextTask();
                    if (next == null)
                    {
                        _isRunning = false;
                        return;
                    }
                    else
                    {
                        var now = DateTimeOffset.UtcNow;
                        if (next.ExecuteAt <= now)
                        {
                            taskToRun = next;
                            _taskMap.TryRemove(taskToRun.Id, out _);
                            _taskQueue.Dequeue();
                            waitTime = TimeSpan.Zero;
                        }
                        else
                        {
                            waitTime = next.ExecuteAt - now;
                        }
                    }
                }

                if (taskToRun != null)
                {
                    bool success = false;
                    try
                    {
                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        await taskToRun.TaskFunc(_provider, taskToRun.Params, cts.Token);
                        success = true;
                    }
                    catch
                    {
                    }

                    lock (_lock)
                    {
                        if (!success && taskToRun.RetryCount > 0)
                        {
                            taskToRun.RetryCount--;
                            var delay = taskToRun.RetryDelay ?? TimeSpan.FromMicroseconds(500);
                            taskToRun.ExecuteAt = DateTimeOffset.UtcNow.Add(delay);
                            _taskMap[taskToRun.Id] = taskToRun;
                            _taskQueue.Enqueue(taskToRun, taskToRun.ExecuteAt);
                            _semaphore.Release();
                        }
                        else if (success && taskToRun.Interval.HasValue)
                        {
                            taskToRun.ExecuteAt = DateTimeOffset.UtcNow.Add(taskToRun.Interval.Value);
                            _taskMap[taskToRun.Id] = taskToRun;
                            _taskQueue.Enqueue(taskToRun, taskToRun.ExecuteAt);
                            _semaphore.Release();
                        }
                    }
                }
                else
                {
                    await _semaphore.WaitAsync(waitTime, stoppingToken);
                }
            }
        }

        public List<ScheduledTask> GetAll()
        {
            return _taskMap.Values.OrderBy(t => t.ExecuteAt).ToList();
        }

        public void Dispose()
        {
            _cts.Cancel();
            _semaphore.Dispose();
            _cts.Dispose();
        }
    }
}
