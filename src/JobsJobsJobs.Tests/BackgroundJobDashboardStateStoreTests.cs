using JobsJobsJobs.Core.BackgroundJobs;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Umbraco.Cms.JobsJobsJobs.Tests.FakeJobs;
using Xunit;

namespace JobsJobsJobs.Tests;

public class BackgroundJobDashboardStateStoreTests
{
    [Fact]
    public void Constructor_IncludesRegisteredJobs_InAlphabeticalOrder()
    {
        BackgroundJobDashboardStateStore store = CreateStore(new IntervalJob(), new StoppableJob());

        var aliases = store.GetAll().Select(x => x.Alias).ToArray();

        Assert.Contains(typeof(IntervalJob).FullName, aliases);
        Assert.Contains(typeof(StoppableJob).FullName, aliases);
        Assert.True(store.TryGet(typeof(StoppableJob).FullName!, out BackgroundJobDashboardItem? stoppableItem));
        Assert.True(stoppableItem.CanStop);
        Assert.True(store.TryGet(typeof(IntervalJob).FullName!, out BackgroundJobDashboardItem? intervalItem));
        Assert.False(intervalItem.CanStop);
    }

    [Fact]
    public void TryGet_ForUnknownAlias_ReturnsFalse()
    {
        BackgroundJobDashboardStateStore store = CreateStore(new IntervalJob());

        Assert.False(store.TryGet("Missing", out _));
    }

    [Fact]
    public void TryBeginExecution_FirstCall_SucceedsButSecondConcurrentCallFails()
    {
        var job = new IntervalJob();
        BackgroundJobDashboardStateStore store = CreateStore(job);

        Assert.True(store.TryBeginExecution(job));
        Assert.False(store.TryBeginExecution(job));
    }

    [Fact]
    public void TryBeginExecution_AfterAbort_AllowsNewExecution()
    {
        var job = new IntervalJob();
        BackgroundJobDashboardStateStore store = CreateStore(job);
        store.TryBeginExecution(job);

        store.AbortExecution(job);

        Assert.True(store.TryBeginExecution(job));
    }

    [Fact]
    public void MarkRunning_SetsLiveState()
    {
        var job = new IntervalJob();
        BackgroundJobDashboardStateStore store = CreateStore(job);
        store.TryBeginExecution(job);

        store.MarkRunning(job);

        Assert.True(store.TryGet(typeof(IntervalJob).FullName!, out BackgroundJobDashboardItem? item));
        Assert.True(item.IsRunning);
        Assert.Equal(BackgroundJobStatus.Running, item.LastStatus);
        Assert.False(item.StopRequested);
        Assert.NotNull(item.LastStartedAt);
    }

    [Fact]
    public void MarkCompleted_ClearsRunningStateAndStoresSuccess()
    {
        var job = new IntervalJob();
        BackgroundJobDashboardStateStore store = CreateStore(job);
        store.TryBeginExecution(job);
        store.MarkRunning(job);
        var messages = new EventMessages();

        store.MarkCompleted(job, BackgroundJobStatus.Succeeded, messages);

        Assert.True(store.TryGet(typeof(IntervalJob).FullName!, out BackgroundJobDashboardItem? item));
        Assert.False(item.IsRunning);
        Assert.Equal(BackgroundJobStatus.Succeeded, item.LastStatus);
        Assert.NotNull(item.LastCompletedAt);
        Assert.NotNull(item.LastSucceededAt);
        Assert.Null(item.LastFailedAt);
    }

    [Fact]
    public void MarkCompleted_WithStateMessage_PopulatesLastMessage()
    {
        var job = new IntervalJob();
        BackgroundJobDashboardStateStore store = CreateStore(job);
        store.TryBeginExecution(job);
        store.MarkRunning(job);
        var messages = new EventMessages();
        var state = new Dictionary<string, object?> { [BackgroundJobDashboardStateKeys.Message] = "done" };

        store.MarkCompleted(job, BackgroundJobStatus.Succeeded, messages, state);

        Assert.True(store.TryGet(typeof(IntervalJob).FullName!, out BackgroundJobDashboardItem? item));
        Assert.Equal("done", item.LastMessage);
    }

    [Fact]
    public void MarkFailed_StoresErrorAndFailedTimestamp()
    {
        var job = new IntervalJob();
        BackgroundJobDashboardStateStore store = CreateStore(job);
        store.TryBeginExecution(job);
        store.MarkRunning(job);
        var messages = new EventMessages();
        var state = new Dictionary<string, object?>
        {
            [BackgroundJobDashboardStateKeys.ErrorMessage] = "boom",
            [BackgroundJobDashboardStateKeys.Message] = "run failed",
        };

        store.MarkFailed(job, messages, state);

        Assert.True(store.TryGet(typeof(IntervalJob).FullName!, out BackgroundJobDashboardItem? item));
        Assert.False(item.IsRunning);
        Assert.Equal(BackgroundJobStatus.Failed, item.LastStatus);
        Assert.Equal("boom", item.LastError);
        Assert.Equal("run failed", item.LastMessage);
        Assert.NotNull(item.LastFailedAt);
        Assert.Null(item.LastSucceededAt);
    }

    [Fact]
    public void MarkStopRequested_SetsStopRequestedFlag()
    {
        var job = new StoppableJob();
        BackgroundJobDashboardStateStore store = CreateStore(job);
        store.TryBeginExecution(job);
        store.MarkRunning(job);

        store.MarkStopRequested(job);

        Assert.True(store.TryGet(typeof(StoppableJob).FullName!, out BackgroundJobDashboardItem? item));
        Assert.True(item.StopRequested);
    }

    [Fact]
    public void AbortExecution_AfterMarkRunning_RestoresIdleStateWhenNoOtherExecutions()
    {
        var job = new IntervalJob();
        BackgroundJobDashboardStateStore store = CreateStore(job);
        store.TryBeginExecution(job);
        store.MarkRunning(job);

        store.AbortExecution(job);

        Assert.True(store.TryGet(typeof(IntervalJob).FullName!, out BackgroundJobDashboardItem? item));
        Assert.False(item.IsRunning);
        Assert.Equal(BackgroundJobStatus.Idle, item.LastStatus);
    }

    [Fact]
    public void Constructor_WhenIncludeUmbracoJobsIsFalse_OmitsUmbracoJobs()
    {
        BackgroundJobDashboardStateStore store = CreateStore(
            new[] { (IRecurringBackgroundJob)new UmbracoNamespacedJob() },
            includeUmbracoJobs: false
        );

        Assert.Empty(store.GetAll());
    }

    [Fact]
    public void Constructor_WhenIncludeUmbracoJobsIsTrue_IncludesUmbracoJobs()
    {
        BackgroundJobDashboardStateStore store = CreateStore(new[] { (IRecurringBackgroundJob)new UmbracoNamespacedJob() }, includeUmbracoJobs: true);

        Assert.Single(store.GetAll());
    }

    [Fact]
    public void Constructor_WhenJobImplementsIInternalBackgroundJob_AlwaysOmitsIt()
    {
        BackgroundJobDashboardStateStore store = CreateStore(new[] { (IRecurringBackgroundJob)new InternalJob() }, includeUmbracoJobs: true);

        Assert.Empty(store.GetAll());
    }

    private sealed class InternalJob : RecurringBackgroundJobBase, IInternalBackgroundJob
    {
        public override TimeSpan Period => TimeSpan.FromMinutes(1);

        public override Task RunJobAsync() => Task.CompletedTask;
    }

    private static BackgroundJobDashboardStateStore CreateStore(params IRecurringBackgroundJob[] jobs) =>
        CreateStore(jobs, includeUmbracoJobs: false);

    private static BackgroundJobDashboardStateStore CreateStore(IEnumerable<IRecurringBackgroundJob> jobs, bool includeUmbracoJobs) =>
        new(
            jobs,
            new BackgroundJobRunExecutionContextAccessor(),
            Options.Create(new BackgroundJobDashboardOptions { IncludeUmbracoJobs = includeUmbracoJobs })
        );

    private sealed class IntervalJob : RecurringBackgroundJobBase
    {
        public override TimeSpan Period => TimeSpan.FromMinutes(1);

        public override Task RunJobAsync() => Task.CompletedTask;
    }

    private sealed class StoppableJob : RecurringBackgroundJobBase, IStoppableRecurringBackgroundJob
    {
        public override TimeSpan Period => TimeSpan.FromMinutes(1);

        public override Task RunJobAsync() => Task.CompletedTask;
    }
}
