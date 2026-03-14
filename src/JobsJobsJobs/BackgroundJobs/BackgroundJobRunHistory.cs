using System;
using System.Collections.Generic;
using System.Threading;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace JobsJobsJobs.BackgroundJobs;

internal sealed class BackgroundJobRunExecutionContext
{
    public Guid RunId { get; init; }

    public string JobAlias { get; init; } = string.Empty;

    public BackgroundJobRunTrigger Trigger { get; init; }

    public DateTime StartedAt { get; init; }
}

internal interface IBackgroundJobRunExecutionContextAccessor
{
    public BackgroundJobRunExecutionContext? Current { get; }

    public BackgroundJobRunExecutionContext Create(IRecurringBackgroundJob job, BackgroundJobRunTrigger trigger);

    public void Set(BackgroundJobRunExecutionContext context);

    public void Clear();
}

internal sealed class BackgroundJobRunExecutionContextAccessor : IBackgroundJobRunExecutionContextAccessor
{
    private readonly AsyncLocal<BackgroundJobRunExecutionContext?> _current = new();

    public BackgroundJobRunExecutionContext? Current => _current.Value;

    public BackgroundJobRunExecutionContext Create(IRecurringBackgroundJob job, BackgroundJobRunTrigger trigger)
        => new()
        {
            RunId = Guid.NewGuid(),
            JobAlias = BackgroundJobDashboardNaming.GetAlias(job),
            Trigger = trigger,
            StartedAt = DateTime.UtcNow,
        };

    public void Set(BackgroundJobRunExecutionContext context) => _current.Value = context;

    public void Clear() => _current.Value = null;
}

public enum BackgroundJobRunTrigger
{
    Automatic,
    Manual,
}

public enum BackgroundJobRunLogLevel
{
    Information,
    Warning,
    Error,
}

public class BackgroundJobRunLogEntry
{
    public DateTime LoggedAt { get; set; }

    public string Level { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}

public class BackgroundJobRunHistoryItem
{
    public Guid Id { get; set; }

    public string JobAlias { get; set; } = string.Empty;

    public string JobName { get; set; } = string.Empty;

    public string Trigger { get; set; } = string.Empty;

    public BackgroundJobStatus Status { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public TimeSpan? Duration { get; set; }

    public string? Message { get; set; }

    public string? Error { get; set; }

    public IReadOnlyCollection<BackgroundJobRunLogEntry> Logs { get; set; } = Array.Empty<BackgroundJobRunLogEntry>();
}

public interface IBackgroundJobRunHistoryService
{
    public IReadOnlyDictionary<string, BackgroundJobRunHistoryItem> GetLatestRuns(IEnumerable<string> aliases, int maxLogsPerRun = 20);
}

public interface IBackgroundJobRunRecorder
{
    public void MarkStarted(IRecurringBackgroundJob job, BackgroundJobRunTrigger trigger);

    public void MarkCompleted(IRecurringBackgroundJob job, BackgroundJobStatus status, EventMessages messages, IDictionary<string, object?>? state = null);

    public void MarkFailed(IRecurringBackgroundJob job, EventMessages messages, IDictionary<string, object?>? state = null);

    public void WriteLog(string alias, BackgroundJobRunLogLevel level, string message);
}

public interface IBackgroundJobRunLogWriter<TJob>
{
    public void Information(string message);

    public void Warning(string message);

    public void Error(string message);
}
