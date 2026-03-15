using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cronos;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Umbraco.Extensions;

namespace JobsJobsJobs.BackgroundJobs;

public interface ICronBackgroundJob
{
    public string CronExpression { get; }

    public TimeZoneInfo TimeZone { get; }

    public TimeSpan PollingPeriod { get; }

    public TimeSpan Delay { get; }

    public ServerRole[] ServerRoles { get; }

    public Task RunJobAsync();
}

public interface IStoppableCronBackgroundJob : ICronBackgroundJob
{
}

public abstract class CronBackgroundJobBase : ICronBackgroundJob
{
    public abstract string CronExpression { get; }

    public virtual TimeZoneInfo TimeZone => TimeZoneInfo.Utc;

    public virtual TimeSpan PollingPeriod => TimeSpan.FromMinutes(1);

    public virtual TimeSpan Delay => PollingPeriod;

    public virtual ServerRole[] ServerRoles => Enum.GetValues<ServerRole>();

    public abstract Task RunJobAsync();
}

public abstract class StoppableCronBackgroundJobBase : CronBackgroundJobBase, IStoppableCronBackgroundJob
{
}

public static class BackgroundJobCronRegistrationExtensions
{
    public static IServiceCollection AddCronBackgroundJob<TJob>(this IServiceCollection services)
        where TJob : class, ICronBackgroundJob
    {
        services.AddTransient<TJob>();
        services.AddRecurringBackgroundJob<CronRecurringBackgroundJobAdapter<TJob>>();
        return services;
    }

    public static IServiceCollection AddStoppableCronBackgroundJob<TJob>(this IServiceCollection services)
        where TJob : class, IStoppableCronBackgroundJob
    {
        services.AddTransient<TJob>();
        services.AddRecurringBackgroundJob<StoppableCronRecurringBackgroundJobAdapter<TJob>>();
        return services;
    }
}

internal sealed class BackgroundJobCronSuppressionCoordinator : IBackgroundJobCronSuppressionCoordinator
{
    private readonly ConcurrentDictionary<string, int> _jobSkips = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _notificationSkips = new(StringComparer.OrdinalIgnoreCase);

    public void Suppress(string alias)
    {
        _jobSkips.AddOrUpdate(alias, 1, (_, value) => value + 1);
        _notificationSkips.AddOrUpdate(alias, 1, (_, value) => value + 1);
    }

    public bool TryConsumeJobSkip(string alias) => TryConsume(_jobSkips, alias);

    public bool TryConsumeNotificationSkip(string alias) => TryConsume(_notificationSkips, alias);

    private static bool TryConsume(ConcurrentDictionary<string, int> dictionary, string alias)
    {
        while (dictionary.TryGetValue(alias, out int count))
        {
            if (count <= 1)
            {
                if (dictionary.TryRemove(alias, out _))
                {
                    return true;
                }

                continue;
            }

            if (dictionary.TryUpdate(alias, count - 1, count))
            {
                return true;
            }
        }

        return false;
    }
}

internal interface IBackgroundJobDashboardMetadata
{
    public Type JobType { get; }

    public bool UsesCronSchedule { get; }

    public string ScheduleDisplay { get; }

    public string? CronExpression { get; }

    public string? TimeZoneId { get; }
}

internal interface ICronRecurringBackgroundJobAdapter : IBackgroundJobDashboardMetadata
{
    public bool ShouldExecute(BackgroundJobRunExecutionContext context);
}

internal interface IBackgroundJobCronScheduler
{
    public bool ShouldExecute(string alias, string cronExpression, TimeZoneInfo timeZone, DateTime startedAtUtc);
}

internal interface IBackgroundJobCronSuppressionCoordinator
{
    public void Suppress(string alias);

    public bool TryConsumeJobSkip(string alias);

    public bool TryConsumeNotificationSkip(string alias);
}

internal sealed class BackgroundJobCronScheduler : IBackgroundJobCronScheduler
{
    private readonly ConcurrentDictionary<string, IReadOnlyList<CronExpression>> _expressions = new(StringComparer.Ordinal);
    private readonly IBackgroundJobRunHistoryService _runHistoryService;
    private readonly DateTime _startedAtUtc;

    public BackgroundJobCronScheduler(IBackgroundJobRunHistoryService runHistoryService)
        : this(runHistoryService, () => DateTime.UtcNow)
    {
    }

    internal BackgroundJobCronScheduler(IBackgroundJobRunHistoryService runHistoryService, Func<DateTime> utcNow)
    {
        _runHistoryService = runHistoryService;
        _startedAtUtc = EnsureUtc(utcNow());
    }

    public bool ShouldExecute(string alias, string cronExpression, TimeZoneInfo timeZone, DateTime startedAtUtc)
    {
        IReadOnlyList<CronExpression> expressions = _expressions.GetOrAdd(cronExpression, ParseExpressions);
        IReadOnlyDictionary<string, BackgroundJobRunHistoryItem> latestAutomaticRuns = _runHistoryService.GetLatestRuns(
            new[] { alias },
            BackgroundJobRunTrigger.Automatic,
            maxLogsPerRun: 0);

        DateTime evaluationTimeUtc = EnsureUtc(startedAtUtc);

        DateTime baseline = latestAutomaticRuns.TryGetValue(alias, out BackgroundJobRunHistoryItem? latestAutomaticRun)
            ? EnsureUtc(latestAutomaticRun.StartedAt)
            : _startedAtUtc.AddTicks(-1);

        return expressions.Any(expression =>
        {
            DateTime? nextOccurrence = expression.GetNextOccurrence(baseline, timeZone);
            return nextOccurrence.HasValue && nextOccurrence.Value <= evaluationTimeUtc;
        });
    }

    private static IReadOnlyList<CronExpression> ParseExpressions(string cronExpression)
    {
        string[] parts = cronExpression
            .Split(new[] { ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
        {
            throw new InvalidOperationException("At least one CRON expression is required.");
        }

        try
        {
            return parts.Select(part => CronExpression.Parse(part, CronFormat.Standard)).ToArray();
        }
        catch (CronFormatException ex)
        {
            throw new InvalidOperationException($"Invalid CRON expression '{cronExpression}'.", ex);
        }
    }

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
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
        IBackgroundJobRunExecutionContextAccessor runExecutionContextAccessor)
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

    public bool ShouldExecute(BackgroundJobRunExecutionContext context)
        => context.Trigger == BackgroundJobRunTrigger.Manual
            || _cronScheduler.ShouldExecute(BackgroundJobDashboardNaming.GetAlias(typeof(TJob)), _job.CronExpression, _job.TimeZone, context.StartedAt);

    public virtual async Task RunJobAsync()
    {
        string alias = BackgroundJobDashboardNaming.GetAlias(typeof(TJob));
        if (_cronSuppressionCoordinator.TryConsumeJobSkip(alias))
        {
            return;
        }

        if (_runExecutionContextAccessor.Current?.ShouldExecute is false)
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
        IBackgroundJobRunExecutionContextAccessor runExecutionContextAccessor)
        : base(job, cronScheduler, cronSuppressionCoordinator, runExecutionContextAccessor)
    {
    }
}
