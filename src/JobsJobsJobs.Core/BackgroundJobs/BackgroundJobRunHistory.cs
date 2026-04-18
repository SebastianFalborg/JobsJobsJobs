using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace JobsJobsJobs.Core.BackgroundJobs;

internal sealed class BackgroundJobRunExecutionContext
{
    public Guid RunId { get; init; }

    public string JobAlias { get; init; } = string.Empty;

    public BackgroundJobRunTrigger Trigger { get; init; }

    public DateTime StartedAt { get; init; }

    public bool ShouldExecute { get; set; } = true;

    public ConcurrentQueue<PendingLogEntry> PendingLogs { get; } = new();

    public PendingRunMetadata? PendingRun { get; set; }
}

internal sealed record PendingLogEntry(BackgroundJobRunLogLevel Level, string Message, DateTime LoggedAt);

internal sealed record PendingRunMetadata(Guid Id, string JobAlias, string JobName, string Trigger, DateTime StartedAt);

internal interface IBackgroundJobRunExecutionContextAccessor
{
    public BackgroundJobRunExecutionContext Create(IRecurringBackgroundJob job, BackgroundJobRunTrigger trigger);

    public void Set(IRecurringBackgroundJob job, BackgroundJobRunExecutionContext context);

    public BackgroundJobRunExecutionContext? Get(IRecurringBackgroundJob job);

    public void Clear(IRecurringBackgroundJob job);
}

internal sealed class BackgroundJobRunExecutionContextAccessor : IBackgroundJobRunExecutionContextAccessor
{
    private readonly ConcurrentDictionary<IRecurringBackgroundJob, BackgroundJobRunExecutionContext> _contexts = new();

    public BackgroundJobRunExecutionContext Create(IRecurringBackgroundJob job, BackgroundJobRunTrigger trigger) =>
        new()
        {
            RunId = Guid.NewGuid(),
            JobAlias = BackgroundJobDashboardNaming.GetAlias(job),
            Trigger = trigger,
            StartedAt = DateTime.UtcNow,
        };

    public void Set(IRecurringBackgroundJob job, BackgroundJobRunExecutionContext context) => _contexts[job] = context;

    public BackgroundJobRunExecutionContext? Get(IRecurringBackgroundJob job) => _contexts.TryGetValue(job, out var context) ? context : null;

    public void Clear(IRecurringBackgroundJob job) => _contexts.TryRemove(job, out _);
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

public abstract class RecurringBackgroundJobBase : IRecurringBackgroundJob
{
    public abstract TimeSpan Period { get; }

    public virtual TimeSpan Delay => Period;

    public virtual ServerRole[] ServerRoles => Enum.GetValues<ServerRole>();

    public virtual event EventHandler? PeriodChanged
    {
        add { }
        remove { }
    }

    public abstract Task RunJobAsync();
}

public interface IStoppableRecurringBackgroundJob : IRecurringBackgroundJob { }

public interface IBackgroundJobExecutionCancellation
{
    public bool IsStopRequested(object job);

    public CancellationToken GetCancellationToken(object job);

    public void ThrowIfCancellationRequested(object job);
}

internal enum BackgroundJobStopRequestState
{
    Success,
    NotRunning,
    NotSupported,
    AlreadyRequested,
}

internal interface IBackgroundJobStopCoordinator
{
    public void Register(IRecurringBackgroundJob job, BackgroundJobRunExecutionContext context);

    public void Complete(Guid runId);

    public BackgroundJobStopRequestState RequestStop(string alias);

    public bool IsStopRequested(Guid runId);

    public bool IsStopRequested(string alias);

    public CancellationToken GetCancellationToken(Guid runId);

    public CancellationToken GetCancellationToken(string alias);
}

internal sealed class BackgroundJobStopCoordinator : IBackgroundJobStopCoordinator
{
    private readonly ConcurrentDictionary<Guid, BackgroundJobActiveExecution> _executions = new();

    public void Register(IRecurringBackgroundJob job, BackgroundJobRunExecutionContext context) =>
        _executions[context.RunId] = new BackgroundJobActiveExecution(
            context.JobAlias,
            job is IStoppableRecurringBackgroundJob,
            new CancellationTokenSource()
        );

    public void Complete(Guid runId)
    {
        if (_executions.TryRemove(runId, out var execution))
        {
            execution.Dispose();
        }
    }

    public BackgroundJobStopRequestState RequestStop(string alias)
    {
        var executions = _executions
            .Where(x => string.Equals(x.Value.JobAlias, alias, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Value)
            .ToArray();

        if (executions.Length == 0)
        {
            return BackgroundJobStopRequestState.NotRunning;
        }

        if (executions.Any(x => x.SupportsStop is false))
        {
            return BackgroundJobStopRequestState.NotSupported;
        }

        var stopRequested = false;

        foreach (var execution in executions)
        {
            stopRequested |= execution.TryRequestStop();
        }

        return stopRequested ? BackgroundJobStopRequestState.Success : BackgroundJobStopRequestState.AlreadyRequested;
    }

    public bool IsStopRequested(Guid runId) => _executions.TryGetValue(runId, out var execution) && execution.IsStopRequested;

    public bool IsStopRequested(string alias) =>
        _executions.Values.Any(x => string.Equals(x.JobAlias, alias, StringComparison.OrdinalIgnoreCase) && x.IsStopRequested);

    public CancellationToken GetCancellationToken(Guid runId) =>
        _executions.TryGetValue(runId, out var execution) ? execution.CancellationTokenSource.Token : CancellationToken.None;

    public CancellationToken GetCancellationToken(string alias) =>
        _executions.Values.FirstOrDefault(x => string.Equals(x.JobAlias, alias, StringComparison.OrdinalIgnoreCase))?.CancellationTokenSource.Token
        ?? CancellationToken.None;
}

internal sealed class BackgroundJobExecutionCancellation : IBackgroundJobExecutionCancellation
{
    private readonly IBackgroundJobStopCoordinator _stopCoordinator;

    public BackgroundJobExecutionCancellation(IBackgroundJobStopCoordinator stopCoordinator)
    {
        _stopCoordinator = stopCoordinator;
    }

    public bool IsStopRequested(object job) => _stopCoordinator.IsStopRequested(GetAlias(job));

    public CancellationToken GetCancellationToken(object job) => _stopCoordinator.GetCancellationToken(GetAlias(job));

    public void ThrowIfCancellationRequested(object job) => GetCancellationToken(job).ThrowIfCancellationRequested();

    private static string GetAlias(object job) =>
        job is IRecurringBackgroundJob recurringJob
            ? BackgroundJobDashboardNaming.GetAlias(recurringJob)
            : BackgroundJobDashboardNaming.GetAlias(job.GetType());
}

internal sealed class BackgroundJobActiveExecution : IDisposable
{
    private int _stopRequested;

    public BackgroundJobActiveExecution(string jobAlias, bool supportsStop, CancellationTokenSource cancellationTokenSource)
    {
        JobAlias = jobAlias;
        SupportsStop = supportsStop;
        CancellationTokenSource = cancellationTokenSource;
    }

    public string JobAlias { get; }

    public bool SupportsStop { get; }

    public CancellationTokenSource CancellationTokenSource { get; }

    public bool IsStopRequested => Volatile.Read(ref _stopRequested) == 1;

    public bool TryRequestStop()
    {
        if (Interlocked.Exchange(ref _stopRequested, 1) == 1)
        {
            return false;
        }

        CancellationTokenSource.Cancel();
        return true;
    }

    public void Dispose() => CancellationTokenSource.Dispose();
}

public record BackgroundJobRunLogEntry
{
    public DateTime LoggedAt { get; init; }

    public string Level { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}

public record BackgroundJobRunHistoryItem
{
    public Guid Id { get; init; }

    public string JobAlias { get; init; } = string.Empty;

    public string JobName { get; init; } = string.Empty;

    public string Trigger { get; init; } = string.Empty;

    public BackgroundJobStatus Status { get; init; }

    public DateTime StartedAt { get; init; }

    public DateTime? CompletedAt { get; init; }

    public TimeSpan? Duration { get; init; }

    public string? Message { get; init; }

    public string? Error { get; init; }

    public IReadOnlyCollection<BackgroundJobRunLogEntry> Logs { get; init; } = Array.Empty<BackgroundJobRunLogEntry>();
}

public interface IBackgroundJobRunHistoryService
{
    public IReadOnlyDictionary<string, BackgroundJobRunHistoryItem> GetLatestRuns(
        IEnumerable<string> aliases,
        BackgroundJobRunTrigger? trigger = null,
        int maxLogsPerRun = 20
    );

    public IReadOnlyDictionary<string, IReadOnlyCollection<BackgroundJobRunHistoryItem>> GetRecentRuns(
        IEnumerable<string> aliases,
        int maxRuns = 5,
        int maxLogsPerRun = 0
    );
}

public interface IBackgroundJobRunRecorder
{
    public void MarkStarted(IRecurringBackgroundJob job, BackgroundJobRunTrigger trigger);

    public void MarkCompleted(
        IRecurringBackgroundJob job,
        BackgroundJobStatus status,
        EventMessages messages,
        IDictionary<string, object?>? state = null
    );

    public void MarkFailed(IRecurringBackgroundJob job, EventMessages messages, IDictionary<string, object?>? state = null);

    public void WriteLog(IRecurringBackgroundJob job, BackgroundJobRunLogLevel level, string message);

    public void WriteLog(Type jobType, BackgroundJobRunLogLevel level, string message);
}

public interface IBackgroundJobRunLogWriter<TJob>
{
    public void Information(TJob job, string message);

    public void Warning(TJob job, string message);

    public void Error(TJob job, string message);
}

internal sealed class BackgroundJobRunLogWriter<TJob> : IBackgroundJobRunLogWriter<TJob>
{
    private readonly IBackgroundJobRunRecorder _runRecorder;

    public BackgroundJobRunLogWriter(IBackgroundJobRunRecorder runRecorder) => _runRecorder = runRecorder;

    public void Information(TJob job, string message) => WriteLog(job, BackgroundJobRunLogLevel.Information, message);

    public void Warning(TJob job, string message) => WriteLog(job, BackgroundJobRunLogLevel.Warning, message);

    public void Error(TJob job, string message) => WriteLog(job, BackgroundJobRunLogLevel.Error, message);

    private void WriteLog(TJob job, BackgroundJobRunLogLevel level, string message)
    {
        if (job is IRecurringBackgroundJob recurringJob)
        {
            _runRecorder.WriteLog(recurringJob, level, message);
            return;
        }

        _runRecorder.WriteLog(typeof(TJob), level, message);
    }
}
