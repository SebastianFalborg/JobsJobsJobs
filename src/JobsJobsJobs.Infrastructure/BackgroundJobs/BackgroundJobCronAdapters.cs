using System;
using System.Threading.Tasks;
using JobsJobsJobs.Core.BackgroundJobs;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace JobsJobsJobs.Infrastructure.BackgroundJobs;

internal class CronRecurringBackgroundJobAdapter<TJob> : IRecurringBackgroundJob, ICronRecurringBackgroundJobAdapter
    where TJob : class, ICronBackgroundJob
{
    private readonly TJob _job;
    private readonly IBackgroundJobCronScheduler _cronScheduler;
    private readonly IBackgroundJobCronSuppressionCoordinator _cronSuppressionCoordinator;
    private readonly IBackgroundJobRunExecutionContextAccessor _runExecutionContextAccessor;

    public CronRecurringBackgroundJobAdapter(
        TJob job,
        IBackgroundJobCronScheduler cronScheduler,
        IBackgroundJobCronSuppressionCoordinator cronSuppressionCoordinator,
        IBackgroundJobRunExecutionContextAccessor runExecutionContextAccessor
    )
    {
        _job = job;
        _cronScheduler = cronScheduler;
        _cronSuppressionCoordinator = cronSuppressionCoordinator;
        _runExecutionContextAccessor = runExecutionContextAccessor;
    }

    public TimeSpan Period => _job.PollingPeriod;

    public TimeSpan Delay => _job.Delay;

    public ServerRole[] ServerRoles => _job.ServerRoles;

    public event EventHandler? PeriodChanged
    {
        add { }
        remove { }
    }

    public Type JobType => typeof(TJob);

    public bool UsesCronSchedule => true;

    public string ScheduleDisplay => _job.CronExpression;

    public string? CronExpression => _job.CronExpression;

    public string? TimeZoneId => _job.TimeZone.Id;

    public bool ShouldExecute(BackgroundJobRunExecutionContext context) =>
        context.Trigger == BackgroundJobRunTrigger.Manual
        || _cronScheduler.ShouldExecute(BackgroundJobDashboardNaming.GetAlias(typeof(TJob)), _job.CronExpression, _job.TimeZone, context.StartedAt);

    public virtual async Task RunJobAsync()
    {
        var alias = BackgroundJobDashboardNaming.GetAlias(typeof(TJob));
        if (_cronSuppressionCoordinator.TryConsumeJobSkip(alias))
        {
            return;
        }

        if (_runExecutionContextAccessor.Get(this)?.ShouldExecute is false)
        {
            return;
        }

        await _job.RunJobAsync();
    }
}

internal sealed class StoppableCronRecurringBackgroundJobAdapter<TJob> : CronRecurringBackgroundJobAdapter<TJob>, IStoppableRecurringBackgroundJob
    where TJob : class, IStoppableCronBackgroundJob
{
    public StoppableCronRecurringBackgroundJobAdapter(
        TJob job,
        IBackgroundJobCronScheduler cronScheduler,
        IBackgroundJobCronSuppressionCoordinator cronSuppressionCoordinator,
        IBackgroundJobRunExecutionContextAccessor runExecutionContextAccessor
    )
        : base(job, cronScheduler, cronSuppressionCoordinator, runExecutionContextAccessor) { }
}
