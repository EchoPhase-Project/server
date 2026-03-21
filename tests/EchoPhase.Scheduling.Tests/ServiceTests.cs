// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Scheduling.Tests
{
    public class ServiceTests : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly DelayedTaskScheduler _scheduler;

        public ServiceTests()
        {
            var services = new ServiceCollection();
            _provider = services.BuildServiceProvider();
            _scheduler = new DelayedTaskScheduler(_provider);
        }

        [Fact]
        public async Task Enqueue_Task_Should_Execute()
        {
            var tcs = new TaskCompletionSource<bool>();
            var ct = TestContext.Current.CancellationToken;

            _scheduler.Enqueue("param", TimeSpan.Zero, async (sp, p, taskCt) =>
            {
                tcs.SetResult(true);
                await Task.CompletedTask;
            });

            var completed = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
            Assert.True(completed);
        }

        [Fact]
        public async Task Task_Should_Not_Execute_Before_Delay()
        {
            var tcs = new TaskCompletionSource<bool>();
            var ct = TestContext.Current.CancellationToken;

            _scheduler.Enqueue("param", TimeSpan.FromSeconds(10), async (sp, p, taskCt) =>
            {
                tcs.SetResult(true);
                await Task.CompletedTask;
            });

            await Task.WhenAny(tcs.Task, Task.Delay(100, ct));
            Assert.False(tcs.Task.IsCompleted);
        }

        [Fact]
        public async Task RetryCount_Should_Retry_On_Failure()
        {
            var attempts = 0;
            var tcs = new TaskCompletionSource<bool>();
            var ct = TestContext.Current.CancellationToken;

            _scheduler.Enqueue("param", TimeSpan.Zero, async (sp, p, taskCt) =>
            {
                attempts++;
                if (attempts < 2)
                    throw new Exception("fail");
                tcs.SetResult(true);
                await Task.CompletedTask;
            }, retryCount: 1);

            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
            Assert.Equal(2, attempts);
        }

        [Fact]
        public async Task Interval_Should_Repeat_Task()
        {
            var executions = 0;
            var tcs = new TaskCompletionSource<bool>();
            var ct = TestContext.Current.CancellationToken;

            _scheduler.Enqueue("param", TimeSpan.Zero, async (sp, p, taskCt) =>
            {
                if (Interlocked.Increment(ref executions) >= 3)
                    tcs.TrySetResult(true);
                await Task.CompletedTask;
            }, interval: TimeSpan.FromMilliseconds(50));

            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
            Assert.True(executions >= 3);
        }

        [Fact]
        public void GetAll_Should_Return_Queued_Tasks()
        {
            var id = _scheduler.Enqueue(
                "param",
                TimeSpan.FromSeconds(1),
                async (sp, p, ct) => await Task.CompletedTask);

            var tasks = _scheduler.GetAll();
            Assert.Contains(tasks, t => t.Id == id);
        }

        public void Dispose()
        {
            _scheduler.Dispose();
            _provider.Dispose();
        }
    }
}
