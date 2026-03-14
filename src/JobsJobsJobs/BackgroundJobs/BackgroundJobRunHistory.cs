using System;
using System.Collections.Generic;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace JobsJobsJobs.BackgroundJobs;

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
    IReadOnlyDictionary<string, BackgroundJobRunHistoryItem> GetLatestRuns(IEnumerable<string> aliases, int maxLogsPerRun = 20);
}

public interface IBackgroundJobRunRecorder
{
    void MarkStarted(IRecurringBackgroundJob job, BackgroundJobRunTrigger trigger);

    void MarkCompleted(IRecurringBackgroundJob job, BackgroundJobStatus status, EventMessages messages, IDictionary<string, object?>? state = null);

    void MarkFailed(IRecurringBackgroundJob job, EventMessages messages, IDictionary<string, object?>? state = null);

    void WriteLog(string alias, BackgroundJobRunLogLevel level, string message);
}

public interface IBackgroundJobRunLogWriter<TJob>
{
    void Information(string message);

    void Warning(string message);

    void Error(string message);
}
