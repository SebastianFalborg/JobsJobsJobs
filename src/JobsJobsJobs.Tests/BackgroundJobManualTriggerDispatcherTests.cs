using JobsJobsJobs.BackgroundJobs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Runtime;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Xunit;

namespace JobsJobsJobs.Tests;

public class BackgroundJobManualTriggerDispatcherTests
{
    [Fact]
    public async Task TriggerAsync_WhenAliasUnknown_ReturnsNotFound()
    {
        DispatcherHarness harness = DispatcherHarness.Create();

        BackgroundJobTriggerResult result = await harness.Dispatcher.TriggerAsync("Missing.Alias");

        Assert.Equal(BackgroundJobTriggerOperationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task TriggerAsync_WhenRuntimeNotRun_ReturnsNotAllowed()
    {
        var job = new SucceedingJob();
        DispatcherHarness harness = DispatcherHarness.Create(job, configure: h => h.RuntimeState.Level.Returns(RuntimeLevel.Install));

        BackgroundJobTriggerResult result = await harness.Dispatcher.TriggerAsync(AliasOf(job));

        Assert.Equal(BackgroundJobTriggerOperationStatus.NotAllowed, result.Status);
    }

    [Fact]
    public async Task TriggerAsync_WhenServerRoleNotAllowed_ReturnsNotAllowed()
    {
        var job = new SingleRoleJob();
        DispatcherHarness harness = DispatcherHarness.Create(
            job,
            configure: h => h.ServerRoleAccessor.CurrentServerRole.Returns(ServerRole.Subscriber)
        );

        BackgroundJobTriggerResult result = await harness.Dispatcher.TriggerAsync(AliasOf(job));

        Assert.Equal(BackgroundJobTriggerOperationStatus.NotAllowed, result.Status);
    }

    [Fact]
    public async Task TriggerAsync_WhenNotMainDom_ReturnsNotAllowed()
    {
        var job = new SucceedingJob();
        DispatcherHarness harness = DispatcherHarness.Create(job, configure: h => h.MainDom.IsMainDom.Returns(false));

        BackgroundJobTriggerResult result = await harness.Dispatcher.TriggerAsync(AliasOf(job));

        Assert.Equal(BackgroundJobTriggerOperationStatus.NotAllowed, result.Status);
    }

    [Fact]
    public async Task TriggerAsync_WhenAlreadyRunning_ReturnsAlreadyRunning()
    {
        var job = new SucceedingJob();
        DispatcherHarness harness = DispatcherHarness.Create(job);
        harness.StateStore.TryBeginExecution(job);

        BackgroundJobTriggerResult result = await harness.Dispatcher.TriggerAsync(AliasOf(job));

        Assert.Equal(BackgroundJobTriggerOperationStatus.AlreadyRunning, result.Status);
    }

    [Fact]
    public async Task TriggerAsync_WhenJobSucceeds_ReturnsSuccess_AndRecordsCompletion()
    {
        var job = new SucceedingJob();
        DispatcherHarness harness = DispatcherHarness.Create(job);

        BackgroundJobTriggerResult result = await harness.Dispatcher.TriggerAsync(AliasOf(job));

        Assert.Equal(BackgroundJobTriggerOperationStatus.Success, result.Status);
        Assert.True(job.WasExecuted);
        harness.RunRecorder.Received(1).MarkStarted(job, BackgroundJobRunTrigger.Manual);
        harness
            .RunRecorder.Received(1)
            .MarkCompleted(
                job,
                BackgroundJobStatus.Succeeded,
                Arg.Any<Umbraco.Cms.Core.Events.EventMessages>(),
                Arg.Any<IDictionary<string, object?>>()
            );
        Assert.True(harness.StateStore.TryGet(AliasOf(job), out BackgroundJobDashboardItem? item));
        Assert.Equal(BackgroundJobStatus.Succeeded, item.LastStatus);
        Assert.False(item.IsRunning);
    }

    [Fact]
    public async Task TriggerAsync_WhenJobThrows_ReturnsFailed_AndRecordsFailure()
    {
        var job = new ThrowingJob();
        DispatcherHarness harness = DispatcherHarness.Create(job);

        BackgroundJobTriggerResult result = await harness.Dispatcher.TriggerAsync(AliasOf(job));

        Assert.Equal(BackgroundJobTriggerOperationStatus.Failed, result.Status);
        harness.RunRecorder.Received(1).MarkFailed(job, Arg.Any<Umbraco.Cms.Core.Events.EventMessages>(), Arg.Any<IDictionary<string, object?>>());
        Assert.True(harness.StateStore.TryGet(AliasOf(job), out BackgroundJobDashboardItem? item));
        Assert.Equal(BackgroundJobStatus.Failed, item.LastStatus);
        Assert.False(item.IsRunning);
    }

    [Fact]
    public async Task TriggerAsync_WhenJobStopsCooperatively_ReturnsSuccess_WithStoppedStatus()
    {
        var job = new StoppingJob();
        DispatcherHarness harness = DispatcherHarness.Create(job);
        job.Configure(() => harness.StopCoordinator.RequestStop(AliasOf(job)));

        BackgroundJobTriggerResult result = await harness.Dispatcher.TriggerAsync(AliasOf(job));

        Assert.Equal(BackgroundJobTriggerOperationStatus.Success, result.Status);
        Assert.True(harness.StateStore.TryGet(AliasOf(job), out BackgroundJobDashboardItem? item));
        Assert.Equal(BackgroundJobStatus.Stopped, item.LastStatus);
    }

    private static string AliasOf(IRecurringBackgroundJob job) => job.GetType().FullName!;

    private sealed class DispatcherHarness
    {
        public required BackgroundJobManualTriggerDispatcher Dispatcher { get; init; }

        public required BackgroundJobDashboardStateStore StateStore { get; init; }

        public required IBackgroundJobRunRecorder RunRecorder { get; init; }

        public required BackgroundJobStopCoordinator StopCoordinator { get; init; }

        public required IRuntimeState RuntimeState { get; init; }

        public required IMainDom MainDom { get; init; }

        public required IServerRoleAccessor ServerRoleAccessor { get; init; }

        public static DispatcherHarness Create(params IRecurringBackgroundJob[] jobs) => Create(jobs, configure: null);

        public static DispatcherHarness Create(IRecurringBackgroundJob job, Action<DispatcherHarness>? configure) => Create(new[] { job }, configure);

        public static DispatcherHarness Create(IReadOnlyList<IRecurringBackgroundJob> jobs, Action<DispatcherHarness>? configure)
        {
            IRuntimeState runtimeState = Substitute.For<IRuntimeState>();
            runtimeState.Level.Returns(RuntimeLevel.Run);

            IMainDom mainDom = Substitute.For<IMainDom>();
            mainDom.IsMainDom.Returns(true);

            IServerRoleAccessor serverRoleAccessor = Substitute.For<IServerRoleAccessor>();
            serverRoleAccessor.CurrentServerRole.Returns(ServerRole.Single);

            IBackgroundJobRunRecorder runRecorder = Substitute.For<IBackgroundJobRunRecorder>();

            var contextAccessor = new BackgroundJobRunExecutionContextAccessor();
            var stopCoordinator = new BackgroundJobStopCoordinator();
            IOptions<BackgroundJobDashboardOptions> options = Options.Create(new BackgroundJobDashboardOptions());
            var stateStore = new BackgroundJobDashboardStateStore(jobs, contextAccessor, options);

            var dispatcher = new BackgroundJobManualTriggerDispatcher(
                jobs,
                stateStore,
                runRecorder,
                stopCoordinator,
                contextAccessor,
                options,
                runtimeState,
                mainDom,
                serverRoleAccessor,
                NullLogger<BackgroundJobManualTriggerDispatcher>.Instance
            );

            var harness = new DispatcherHarness
            {
                Dispatcher = dispatcher,
                StateStore = stateStore,
                RunRecorder = runRecorder,
                StopCoordinator = stopCoordinator,
                RuntimeState = runtimeState,
                MainDom = mainDom,
                ServerRoleAccessor = serverRoleAccessor,
            };

            configure?.Invoke(harness);
            return harness;
        }
    }

    private sealed class SucceedingJob : RecurringBackgroundJobBase
    {
        public override TimeSpan Period => TimeSpan.FromMinutes(1);

        public bool WasExecuted { get; private set; }

        public override Task RunJobAsync()
        {
            WasExecuted = true;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingJob : RecurringBackgroundJobBase
    {
        public override TimeSpan Period => TimeSpan.FromMinutes(1);

        public override Task RunJobAsync() => throw new InvalidOperationException("boom");
    }

    private sealed class SingleRoleJob : RecurringBackgroundJobBase
    {
        public override TimeSpan Period => TimeSpan.FromMinutes(1);

        public override ServerRole[] ServerRoles => new[] { ServerRole.Single };

        public override Task RunJobAsync() => Task.CompletedTask;
    }

    private sealed class StoppingJob : RecurringBackgroundJobBase, IStoppableRecurringBackgroundJob
    {
        private Action? _onRun;

        public override TimeSpan Period => TimeSpan.FromMinutes(1);

        public void Configure(Action onRun) => _onRun = onRun;

        public override Task RunJobAsync()
        {
            _onRun?.Invoke();
            return Task.CompletedTask;
        }
    }
}
