using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cronos;
using Umbraco.Cms.Core.Sync;

namespace JobsJobsJobs.Core.BackgroundJobs;

public interface ICronBackgroundJob
{
    public string CronExpression { get; }

    public TimeZoneInfo TimeZone { get; }

    public TimeSpan PollingPeriod { get; }

    public TimeSpan Delay { get; }

    public ServerRole[] ServerRoles { get; }

    public Task RunJobAsync();
}

public interface IStoppableCronBackgroundJob : ICronBackgroundJob { }

public abstract class CronBackgroundJobBase : ICronBackgroundJob
{
    public abstract string CronExpression { get; }

    public virtual TimeZoneInfo TimeZone => TimeZoneInfo.Utc;

    public virtual TimeSpan PollingPeriod => TimeSpan.FromMinutes(1);

    public virtual TimeSpan Delay => PollingPeriod;

    public virtual ServerRole[] ServerRoles => Enum.GetValues<ServerRole>();

    public abstract Task RunJobAsync();
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
        while (dictionary.TryGetValue(alias, out var count))
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
    private readonly ConcurrentDictionary<string, DateTime> _baselineByAlias = new(StringComparer.OrdinalIgnoreCase);
    private readonly IBackgroundJobRunHistoryService _runHistoryService;
    private readonly DateTime _startedAtUtc;

    public BackgroundJobCronScheduler(IBackgroundJobRunHistoryService runHistoryService)
        : this(runHistoryService, () => DateTime.UtcNow) { }

    internal BackgroundJobCronScheduler(IBackgroundJobRunHistoryService runHistoryService, Func<DateTime> utcNow)
    {
        _runHistoryService = runHistoryService;
        _startedAtUtc = EnsureUtc(utcNow());
    }

    public bool ShouldExecute(string alias, string cronExpression, TimeZoneInfo timeZone, DateTime startedAtUtc)
    {
        var expressions = _expressions.GetOrAdd(cronExpression, ParseExpressions);
        var evaluationTimeUtc = EnsureUtc(startedAtUtc);
        var baseline = _baselineByAlias.GetOrAdd(alias, LoadBaselineFromHistory);

        var shouldExecute = expressions.Any(expression =>
        {
            var nextOccurrence = expression.GetNextOccurrence(baseline, timeZone);
            return nextOccurrence.HasValue && nextOccurrence.Value <= evaluationTimeUtc;
        });

        if (shouldExecute)
        {
            _baselineByAlias[alias] = evaluationTimeUtc;
        }

        return shouldExecute;
    }

    private DateTime LoadBaselineFromHistory(string alias)
    {
        var latestAutomaticRuns = _runHistoryService.GetLatestRuns(new[] { alias }, BackgroundJobRunTrigger.Automatic, maxLogsPerRun: 0);

        return latestAutomaticRuns.TryGetValue(alias, out var latestAutomaticRun)
            ? EnsureUtc(latestAutomaticRun.StartedAt)
            : _startedAtUtc.AddTicks(-1);
    }

    private static IReadOnlyList<CronExpression> ParseExpressions(string cronExpression)
    {
        var parts = cronExpression.Split(new[] { ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
}
