using System;
using System.Reflection;
using System.Threading.Tasks;
using JobsJobsJobs.Core.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Umbraco.Extensions;

namespace JobsJobsJobs.BackgroundJobs;

public static class BackgroundJobCronRegistrationExtensions
{
    private static readonly MethodInfo s_addStoppableCronBackgroundJobCoreMethod = typeof(BackgroundJobCronRegistrationExtensions).GetMethod(
        nameof(AddStoppableCronBackgroundJobCore),
        BindingFlags.NonPublic | BindingFlags.Static
    )!;

    public static IUmbracoBuilder AddCronBackgroundJob<TJob>(this IUmbracoBuilder builder)
        where TJob : class, ICronBackgroundJob
    {
        builder.Services.AddCronBackgroundJob<TJob>();
        return builder;
    }

    public static IUmbracoBuilder AddStoppableCronBackgroundJob<TJob>(this IUmbracoBuilder builder)
        where TJob : class, IStoppableCronBackgroundJob
    {
        builder.Services.AddCronBackgroundJob<TJob>();
        return builder;
    }

    public static IServiceCollection AddCronBackgroundJob<TJob>(this IServiceCollection services)
        where TJob : class, ICronBackgroundJob
    {
        services.AddTransient<TJob>();

        if (typeof(IStoppableCronBackgroundJob).IsAssignableFrom(typeof(TJob)))
        {
            s_addStoppableCronBackgroundJobCoreMethod.MakeGenericMethod(typeof(TJob)).Invoke(null, new object[] { services });
            return services;
        }

        services.AddRecurringBackgroundJob<CronRecurringBackgroundJobAdapter<TJob>>();
        return services;
    }

    public static IServiceCollection AddStoppableCronBackgroundJob<TJob>(this IServiceCollection services)
        where TJob : class, IStoppableCronBackgroundJob
    {
        return services.AddCronBackgroundJob<TJob>();
    }

    private static void AddStoppableCronBackgroundJobCore<TJob>(IServiceCollection services)
        where TJob : class, IStoppableCronBackgroundJob => services.AddRecurringBackgroundJob<StoppableCronRecurringBackgroundJobAdapter<TJob>>();
}

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
