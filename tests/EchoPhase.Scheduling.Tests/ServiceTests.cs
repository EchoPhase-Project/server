using EchoPhase.Scheduling;
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
        public async Task Task_Should_Not_Execute_Before_Delay()
        {
            var executed = false;

            _scheduler.Enqueue(
                "param",
                TimeSpan.FromSeconds(2),
                async (sp, p, ct) =>
                {
                    executed = true;
                    await Task.CompletedTask;
                });

            await Task.Delay(250, TestContext.Current.CancellationToken);

            Assert.False(executed);
        }

        [Fact]
        public async Task Enqueue_Task_Should_Execute()
        {
            var executed = false;

            _scheduler.Enqueue(
                "param",
                TimeSpan.Zero,
                async (sp, p, ct) =>
                {
                    executed = true;
                    await Task.CompletedTask;
                });

            await Task.Delay(200, TestContext.Current.CancellationToken);
            Assert.True(executed);
        }

        [Fact]
        public async Task Update_Task_Should_Change_ExecutionTime()
        {
            var executed = false;

            var id = _scheduler.Enqueue(
                "param",
                TimeSpan.FromSeconds(5),
                async (sp, p, ct) =>
                {
                    executed = true;
                    await Task.CompletedTask;
                });

            var updated = _scheduler.Update(id, "newParam", TimeSpan.Zero);
            Assert.True(updated);

            await Task.Delay(300, TestContext.Current.CancellationToken);
            Assert.True(executed);
        }

        [Fact]
        public async Task Remove_Task_Should_Prevent_Execution()
        {
            var executed = false;

            var id = _scheduler.Enqueue(
                "param",
                TimeSpan.FromMilliseconds(50),
                async (sp, p, ct) =>
                {
                    executed = true;
                    await Task.CompletedTask;
                });

            var removed = _scheduler.Remove(id);
            Assert.True(removed);

            await Task.Delay(200, TestContext.Current.CancellationToken);
            Assert.False(executed);
        }

        [Fact]
        public async Task RetryCount_Should_Retry_On_Failure()
        {
            var attempts = 0;

            _scheduler.Enqueue(
                "param",
                TimeSpan.Zero,
                async (sp, p, ct) =>
                {
                    attempts++;
                    if (attempts < 2)
                        throw new Exception("fail");
                    await Task.CompletedTask;
                },
                retryCount: 1);

            await Task.Delay(500, TestContext.Current.CancellationToken);
            Assert.Equal(2, attempts);
        }

        [Fact]
        public async Task Interval_Should_Repeat_Task()
        {
            var executions = 0;

            _scheduler.Enqueue(
                "param",
                TimeSpan.Zero,
                async (sp, p, ct) =>
                {
                    executions++;
                    await Task.CompletedTask;
                },
                interval: TimeSpan.FromMilliseconds(100));

            await Task.Delay(350, TestContext.Current.CancellationToken);
            Assert.True(executions >= 2);
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
