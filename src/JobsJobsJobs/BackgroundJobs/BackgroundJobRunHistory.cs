using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace JobsJobsJobs.BackgroundJobs;

internal sealed class BackgroundJobRunExecutionContext
{
    public Guid RunId { get; init; }

    public string JobAlias { get; init; } = string.Empty;

    public BackgroundJobRunTrigger Trigger { get; init; }

    public DateTime StartedAt { get; init; }

    public bool ShouldExecute { get; set; } = true;
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

    public BackgroundJobRunExecutionContext Create(IRecurringBackgroundJob job, BackgroundJobRunTrigger trigger) =>
        new()
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
    public bool IsStopRequested { get; }

    public CancellationToken CancellationToken { get; }

    public void ThrowIfCancellationRequested();
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

    public CancellationToken GetCancellationToken(Guid runId);
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

    public CancellationToken GetCancellationToken(Guid runId) =>
        _executions.TryGetValue(runId, out var execution) ? execution.CancellationTokenSource.Token : CancellationToken.None;
}

internal sealed class BackgroundJobExecutionCancellation : IBackgroundJobExecutionCancellation
{
    private readonly IBackgroundJobRunExecutionContextAccessor _runExecutionContextAccessor;
    private readonly IBackgroundJobStopCoordinator _stopCoordinator;

    public BackgroundJobExecutionCancellation(
        IBackgroundJobRunExecutionContextAccessor runExecutionContextAccessor,
        IBackgroundJobStopCoordinator stopCoordinator
    )
    {
        _runExecutionContextAccessor = runExecutionContextAccessor;
        _stopCoordinator = stopCoordinator;
    }

    public bool IsStopRequested
    {
        get
        {
            var context = _runExecutionContextAccessor.Current;
            return context is not null && _stopCoordinator.IsStopRequested(context.RunId);
        }
    }

    public CancellationToken CancellationToken
    {
        get
        {
            var context = _runExecutionContextAccessor.Current;
            return context is not null ? _stopCoordinator.GetCancellationToken(context.RunId) : CancellationToken.None;
        }
    }

    public void ThrowIfCancellationRequested() => CancellationToken.ThrowIfCancellationRequested();
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

    public void WriteLog(string alias, BackgroundJobRunLogLevel level, string message);
}

public interface IBackgroundJobRunLogWriter<TJob>
{
    public void Information(string message);

    public void Warning(string message);

    public void Error(string message);
}
