using JobsJobsJobs.Core.BackgroundJobs;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Xunit;

namespace JobsJobsJobs.Tests;

public class BackgroundJobStopCoordinatorTests
{
    [Fact]
    public void RequestStop_WhenJobNotRegistered_ReturnsNotRunning()
    {
        var coordinator = new BackgroundJobStopCoordinator();

        BackgroundJobStopRequestState result = coordinator.RequestStop("Unknown.Job");

        Assert.Equal(BackgroundJobStopRequestState.NotRunning, result);
    }

    [Fact]
    public void RequestStop_WhenJobDoesNotSupportStop_ReturnsNotSupported()
    {
        var coordinator = new BackgroundJobStopCoordinator();
        var job = new NonStoppableJob();
        BackgroundJobRunExecutionContext context = CreateContext(job);
        coordinator.Register(job, context);

        BackgroundJobStopRequestState result = coordinator.RequestStop(context.JobAlias);

        Assert.Equal(BackgroundJobStopRequestState.NotSupported, result);
    }

    [Fact]
    public void RequestStop_WhenFirstCall_ReturnsSuccess_AndCancelsToken()
    {
        var coordinator = new BackgroundJobStopCoordinator();
        var job = new StoppableJob();
        BackgroundJobRunExecutionContext context = CreateContext(job);
        coordinator.Register(job, context);

        BackgroundJobStopRequestState result = coordinator.RequestStop(context.JobAlias);

        Assert.Equal(BackgroundJobStopRequestState.Success, result);
        Assert.True(coordinator.IsStopRequested(context.RunId));
        Assert.True(coordinator.GetCancellationToken(context.RunId).IsCancellationRequested);
    }

    [Fact]
    public void RequestStop_WhenCalledTwice_ReturnsAlreadyRequested()
    {
        var coordinator = new BackgroundJobStopCoordinator();
        var job = new StoppableJob();
        BackgroundJobRunExecutionContext context = CreateContext(job);
        coordinator.Register(job, context);

        coordinator.RequestStop(context.JobAlias);
        BackgroundJobStopRequestState secondResult = coordinator.RequestStop(context.JobAlias);

        Assert.Equal(BackgroundJobStopRequestState.AlreadyRequested, secondResult);
    }

    [Fact]
    public void IsStopRequested_ByAlias_ReturnsTrueAfterRequest()
    {
        var coordinator = new BackgroundJobStopCoordinator();
        var job = new StoppableJob();
        BackgroundJobRunExecutionContext context = CreateContext(job);
        coordinator.Register(job, context);
        coordinator.RequestStop(context.JobAlias);

        Assert.True(coordinator.IsStopRequested(context.JobAlias));
    }

    [Fact]
    public void Complete_RemovesExecution_AndFurtherStopReturnsNotRunning()
    {
        var coordinator = new BackgroundJobStopCoordinator();
        var job = new StoppableJob();
        BackgroundJobRunExecutionContext context = CreateContext(job);
        coordinator.Register(job, context);

        coordinator.Complete(context.RunId);

        Assert.False(coordinator.IsStopRequested(context.RunId));
        Assert.Equal(BackgroundJobStopRequestState.NotRunning, coordinator.RequestStop(context.JobAlias));
    }

    [Fact]
    public void GetCancellationToken_ForUnknownRunId_ReturnsNone()
    {
        var coordinator = new BackgroundJobStopCoordinator();

        Assert.Equal(CancellationToken.None, coordinator.GetCancellationToken(Guid.NewGuid()));
    }

    [Fact]
    public void RequestStop_CancelsAllMatchingExecutions()
    {
        var coordinator = new BackgroundJobStopCoordinator();
        var first = new StoppableJob();
        var second = new StoppableJob();
        BackgroundJobRunExecutionContext firstContext = CreateContext(first);
        BackgroundJobRunExecutionContext secondContext = CreateContext(second);
        coordinator.Register(first, firstContext);
        coordinator.Register(second, secondContext);

        BackgroundJobStopRequestState result = coordinator.RequestStop(firstContext.JobAlias);

        Assert.Equal(BackgroundJobStopRequestState.Success, result);
        Assert.True(coordinator.GetCancellationToken(firstContext.RunId).IsCancellationRequested);
        Assert.True(coordinator.GetCancellationToken(secondContext.RunId).IsCancellationRequested);
    }

    private static BackgroundJobRunExecutionContext CreateContext(IRecurringBackgroundJob job) =>
        new()
        {
            RunId = Guid.NewGuid(),
            JobAlias = job.GetType().FullName ?? job.GetType().Name,
            Trigger = BackgroundJobRunTrigger.Manual,
            StartedAt = DateTime.UtcNow,
        };

    private sealed class StoppableJob : RecurringBackgroundJobBase, IStoppableRecurringBackgroundJob
    {
        public override TimeSpan Period => TimeSpan.FromMinutes(1);

        public override Task RunJobAsync() => Task.CompletedTask;
    }

    private sealed class NonStoppableJob : RecurringBackgroundJobBase
    {
        public override TimeSpan Period => TimeSpan.FromMinutes(1);

        public override Task RunJobAsync() => Task.CompletedTask;
    }
}
