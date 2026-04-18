using JobsJobsJobs.Core.BackgroundJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NPoco;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Umbraco.Cms.Infrastructure.Scoping;

namespace JobsJobsJobs.BackgroundJobs;

internal sealed class BackgroundJobRunStore : IBackgroundJobRunHistoryService, IBackgroundJobRunRecorder
{
    private static readonly TimeSpan[] s_writeRetryDelays = { TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(500) };
    private static readonly SemaphoreSlim s_writeSemaphore = new(1, 1);
    private readonly ILogger<BackgroundJobRunStore> _logger;
    private readonly BackgroundJobDashboardOptions _options;
    private readonly IBackgroundJobRunExecutionContextAccessor _runExecutionContextAccessor;
    private readonly IScopeProvider _scopeProvider;

    public BackgroundJobRunStore(
        ILogger<BackgroundJobRunStore> logger,
        IScopeProvider scopeProvider,
        IBackgroundJobRunExecutionContextAccessor runExecutionContextAccessor,
        IOptions<BackgroundJobDashboardOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        _scopeProvider = scopeProvider;
        _runExecutionContextAccessor = runExecutionContextAccessor;
    }

    public void MarkStarted(IRecurringBackgroundJob job, BackgroundJobRunTrigger trigger)
    {
        if (_options.DisablePersistence || ShouldInclude(job) is false)
        {
            return;
        }

        var context = _runExecutionContextAccessor.Get(job);
        if (context is null)
        {
            return;
        }

        var alias = BackgroundJobDashboardNaming.GetAlias(job);

        LogRunStarted(alias, trigger);

        context.PendingRun = new PendingRunMetadata(
            context.RunId,
            alias,
            BackgroundJobDashboardNaming.GetDisplayName(job),
            context.Trigger.ToString(),
            context.StartedAt);
    }

    public void MarkCompleted(IRecurringBackgroundJob job, BackgroundJobStatus status, EventMessages messages, IDictionary<string, object?>? state = null)
    {
        var alias = BackgroundJobDashboardNaming.GetAlias(job);
        var context = _runExecutionContextAccessor.Get(job);
        if (_options.DisablePersistence || ShouldInclude(alias) is false)
        {
            return;
        }

        var completedAt = DateTime.UtcNow;
        var message = ResolveMessage(messages, state);
        var error = status == BackgroundJobStatus.Failed ? ResolveError(messages, state) : null;
        LogRunCompleted(alias, status, message, error);

        var pendingRun = context?.PendingRun;
        var pendingLogs = context is not null ? DrainPendingLogs(context) : Array.Empty<PendingLogEntry>();

        TryExecuteWrite(alias, "persist background job run", () =>
        {
            s_writeSemaphore.Wait();
            try
            {
                using var scope = _scopeProvider.CreateScope(autoComplete: true);

                BackgroundJobRunDto run;
                if (pendingRun is not null)
                {
                    run = new BackgroundJobRunDto
                    {
                        Id = pendingRun.Id,
                        JobAlias = pendingRun.JobAlias,
                        JobName = pendingRun.JobName,
                        Trigger = pendingRun.Trigger,
                        Status = status.ToString(),
                        StartedAt = pendingRun.StartedAt,
                        CompletedAt = completedAt,
                        DurationMs = (long)Math.Max(0, (completedAt - pendingRun.StartedAt).TotalMilliseconds),
                        Message = message ?? string.Empty,
                        Error = error ?? string.Empty,
                    };
                    scope.Database.Insert(run);
                }
                else
                {
                    var runId = _runExecutionContextAccessor.Get(job)?.RunId;
                    var existing = runId.HasValue
                        ? scope.Database.SingleOrDefaultById<BackgroundJobRunDto>(runId.Value)
                        : GetLatestRunningRun(scope.Database, alias);
                    if (existing is null)
                    {
                        return;
                    }

                    existing.Status = status.ToString();
                    existing.CompletedAt = completedAt;
                    existing.DurationMs = (long)Math.Max(0, (completedAt - existing.StartedAt).TotalMilliseconds);
                    existing.Message = message ?? string.Empty;
                    existing.Error = error ?? string.Empty;
                    scope.Database.Update(existing);
                    run = existing;
                }

                foreach (var entry in pendingLogs)
                {
                    InsertLog(scope.Database, run.Id, entry.Level, entry.Message, entry.LoggedAt);
                }

                if (string.IsNullOrWhiteSpace(message) is false)
                {
                    InsertLog(scope.Database, run.Id, BackgroundJobRunLogLevel.Information, message!);
                }

                if (string.IsNullOrWhiteSpace(error) is false)
                {
                    InsertLog(scope.Database, run.Id, BackgroundJobRunLogLevel.Error, error!);
                }
            }
            finally
            {
                s_writeSemaphore.Release();
            }
        });
    }

    public void MarkFailed(IRecurringBackgroundJob job, EventMessages messages, IDictionary<string, object?>? state = null)
        => MarkCompleted(job, BackgroundJobStatus.Failed, messages, state);

    public void WriteLog(IRecurringBackgroundJob job, BackgroundJobRunLogLevel level, string message)
    {
        var alias = BackgroundJobDashboardNaming.GetAlias(job);
        var context = _runExecutionContextAccessor.Get(job);

        WriteLogCore(alias, context, level, message);
    }

    public void WriteLog(Type jobType, BackgroundJobRunLogLevel level, string message)
    {
        var alias = BackgroundJobDashboardNaming.GetAlias(jobType);

        WriteLogCore(alias, null, level, message);
    }

    public IReadOnlyDictionary<string, BackgroundJobRunHistoryItem> GetLatestRuns(IEnumerable<string> aliases, BackgroundJobRunTrigger? trigger = null, int maxLogsPerRun = 20)
    {
        var result = new Dictionary<string, BackgroundJobRunHistoryItem>(StringComparer.OrdinalIgnoreCase);

        TryExecuteRead("read latest background job runs", () =>
        {
            using var scope = _scopeProvider.CreateScope(autoComplete: true);

            foreach (var alias in aliases.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var runs = trigger.HasValue
                    ? scope.Database.Fetch<BackgroundJobRunDto>(
                        "SELECT * FROM JobsJobsJobsBackgroundJobRun WHERE JobAlias = @0 AND Trigger = @1 ORDER BY StartedAt DESC",
                        alias,
                        trigger.Value.ToString())
                    : scope.Database.Fetch<BackgroundJobRunDto>(
                        "SELECT * FROM JobsJobsJobsBackgroundJobRun WHERE JobAlias = @0 ORDER BY StartedAt DESC",
                        alias);

                var run = runs.FirstOrDefault();
                if (run is null)
                {
                    continue;
                }

                result[alias] = MapRun(scope.Database, run, maxLogsPerRun);
            }
        });

        return result;
    }

    public IReadOnlyDictionary<string, IReadOnlyCollection<BackgroundJobRunHistoryItem>> GetRecentRuns(
        IEnumerable<string> aliases,
        int maxRuns = 5,
        int maxLogsPerRun = 0)
    {
        var result = new Dictionary<string, IReadOnlyCollection<BackgroundJobRunHistoryItem>>(StringComparer.OrdinalIgnoreCase);

        TryExecuteRead("read recent background job runs", () =>
        {
            using var scope = _scopeProvider.CreateScope(autoComplete: true);

            foreach (var alias in aliases.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var runs = scope.Database.Fetch<BackgroundJobRunDto>(
                    "SELECT * FROM JobsJobsJobsBackgroundJobRun WHERE JobAlias = @0 ORDER BY StartedAt DESC",
                    alias)
                    .Take(Math.Max(0, maxRuns))
                    .ToList();

                if (runs.Count == 0)
                {
                    continue;
                }

                result[alias] = runs
                    .Select(run => MapRun(scope.Database, run, maxLogsPerRun))
                    .ToArray();
            }
        });

        return result;
    }

    private static void InsertLog(IDatabase database, Guid runId, BackgroundJobRunLogLevel level, string message, DateTime? loggedAt = null)
        => database.Insert(new BackgroundJobRunLogDto
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            Level = level.ToString(),
            Message = message,
            LoggedAt = loggedAt ?? DateTime.UtcNow,
        });

    private static PendingLogEntry[] DrainPendingLogs(BackgroundJobRunExecutionContext context)
    {
        var entries = new List<PendingLogEntry>();
        while (context.PendingLogs.TryDequeue(out var entry))
        {
            entries.Add(entry);
        }

        return entries.ToArray();
    }

    private static BackgroundJobRunDto? GetLatestRunningRun(IDatabase database, string alias)
        => database.Fetch<BackgroundJobRunDto>(
                "SELECT * FROM JobsJobsJobsBackgroundJobRun WHERE JobAlias = @0 AND Status = @1 ORDER BY StartedAt DESC",
                alias,
                BackgroundJobStatus.Running.ToString())
            .FirstOrDefault();

    private static BackgroundJobRunHistoryItem MapRun(IDatabase database, BackgroundJobRunDto run, int maxLogsPerRun)
    {
        var status = Enum.TryParse(run.Status, out BackgroundJobStatus parsedStatus) ? parsedStatus : BackgroundJobStatus.Idle;
        var logs = maxLogsPerRun > 0
            ? database.Fetch<BackgroundJobRunLogDto>(
                "SELECT * FROM JobsJobsJobsBackgroundJobRunLog WHERE RunId = @0 ORDER BY LoggedAt ASC",
                run.Id)
                .Take(maxLogsPerRun)
            : Array.Empty<BackgroundJobRunLogDto>();

        return new BackgroundJobRunHistoryItem
        {
            Id = run.Id,
            JobAlias = run.JobAlias,
            JobName = run.JobName,
            Trigger = run.Trigger,
            Status = status,
            StartedAt = run.StartedAt,
            CompletedAt = status == BackgroundJobStatus.Running ? null : run.CompletedAt,
            Duration = status == BackgroundJobStatus.Running
                ? null
                : run.DurationMs.HasValue ? TimeSpan.FromMilliseconds(run.DurationMs.Value) : null,
            Message = NormalizeStoredText(run.Message),
            Error = NormalizeStoredText(run.Error),
            Logs = logs.Select(x => new BackgroundJobRunLogEntry
            {
                LoggedAt = x.LoggedAt,
                Level = x.Level,
                Message = x.Message,
            }).ToArray(),
        };
    }

    private static string? NormalizeStoredText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private void WriteLogCore(string alias, BackgroundJobRunExecutionContext? context, BackgroundJobRunLogLevel level, string message)
    {
        if (string.IsNullOrWhiteSpace(message) || ShouldInclude(alias) is false)
        {
            return;
        }

        WriteApplicationLog(alias, level, message);

        if (_options.DisablePersistence)
        {
            return;
        }

        if (context is not null)
        {
            context.PendingLogs.Enqueue(new PendingLogEntry(level, message, DateTime.UtcNow));
            return;
        }

        TryExecuteWrite(alias, "persist background job run log", () =>
        {
            using var scope = _scopeProvider.CreateScope(autoComplete: true);
            var runId = GetLatestRunningRun(scope.Database, alias)?.Id;
            if (runId is null)
            {
                return;
            }

            InsertLog(scope.Database, runId.Value, level, message);
        });
    }

    private void LogRunStarted(string alias, BackgroundJobRunTrigger trigger)
    {
        if (_options.MirrorBackgroundJobLogsToUmbracoLog is false)
        {
            return;
        }

        _logger.LogInformation("Background job {JobAlias} started with trigger {Trigger}.", alias, trigger);
    }

    private void LogRunCompleted(string alias, BackgroundJobStatus status, string? message, string? error)
    {
        if (_options.MirrorBackgroundJobLogsToUmbracoLog is false)
        {
            return;
        }

        var detail = error ?? message;

        switch (status)
        {
            case BackgroundJobStatus.Failed:
                _logger.LogError("Background job {JobAlias} failed. {Detail}", alias, detail ?? "No details were provided.");
                break;
            case BackgroundJobStatus.Stopped:
                _logger.LogWarning("Background job {JobAlias} stopped. {Detail}", alias, detail ?? "No details were provided.");
                break;
            case BackgroundJobStatus.Ignored:
                _logger.LogInformation("Background job {JobAlias} was ignored. {Detail}", alias, detail ?? "No details were provided.");
                break;
            default:
                _logger.LogInformation("Background job {JobAlias} completed with status {Status}. {Detail}", alias, status, detail ?? "No details were provided.");
                break;
        }
    }

    private void WriteApplicationLog(string alias, BackgroundJobRunLogLevel level, string message)
    {
        if (_options.MirrorBackgroundJobLogsToUmbracoLog is false)
        {
            return;
        }

        switch (level)
        {
            case BackgroundJobRunLogLevel.Warning:
                _logger.LogWarning("Background job {JobAlias}: {Message}", alias, message);
                break;
            case BackgroundJobRunLogLevel.Error:
                _logger.LogError("Background job {JobAlias}: {Message}", alias, message);
                break;
            default:
                _logger.LogInformation("Background job {JobAlias}: {Message}", alias, message);
                break;
        }
    }

    private void TryExecuteWrite(string alias, string operation, Action action)
    {
        Exception? lastException = null;

        for (var attempt = 0; attempt <= s_writeRetryDelays.Length; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (attempt == s_writeRetryDelays.Length)
                {
                    break;
                }

                Thread.Sleep(s_writeRetryDelays[attempt]);
            }
        }

        _logger.LogWarning(lastException, "Failed to {Operation} for background job {JobAlias} after {AttemptCount} attempts. Continuing without persisted history update.", operation, alias, s_writeRetryDelays.Length + 1);
    }

    private void TryExecuteRead(string operation, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to {Operation}. Returning partial or empty background job history.", operation);
        }
    }

    private bool ShouldInclude(IRecurringBackgroundJob job) => BackgroundJobDashboardNaming.ShouldInclude(job, _options);

    private bool ShouldInclude(string alias) => BackgroundJobDashboardNaming.ShouldInclude(alias, _options);

    private static string? ResolveMessage(EventMessages messages, IDictionary<string, object?>? state)
    {
        if (state is not null && state.TryGetValue(BackgroundJobDashboardStateKeys.Message, out var stateMessage) && stateMessage is string message)
        {
            return message;
        }

        return messages.GetAll().Select(x => x.Message).FirstOrDefault(x => string.IsNullOrWhiteSpace(x) is false);
    }

    private static string? ResolveError(EventMessages messages, IDictionary<string, object?>? state)
    {
        if (state is not null && state.TryGetValue(BackgroundJobDashboardStateKeys.ErrorMessage, out var stateError) && stateError is string error)
        {
            return error;
        }

        return messages.GetAll().Select(x => x.Message).FirstOrDefault(x => string.IsNullOrWhiteSpace(x) is false);
    }
}

[TableName(BackgroundJobRunDto.TableName)]
[PrimaryKey(nameof(BackgroundJobRunDto.Id), AutoIncrement = false)]
[ExplicitColumns]
internal class BackgroundJobRunDto
{
    public const string TableName = "JobsJobsJobsBackgroundJobRun";

    [Column(nameof(Id))]
    public Guid Id { get; set; }

    [Column(nameof(JobAlias))]
    public string JobAlias { get; set; } = string.Empty;

    [Column(nameof(JobName))]
    public string JobName { get; set; } = string.Empty;

    [Column(nameof(Trigger))]
    public string Trigger { get; set; } = string.Empty;

    [Column(nameof(Status))]
    public string Status { get; set; } = string.Empty;

    [Column(nameof(StartedAt))]
    public DateTime StartedAt { get; set; }

    [Column(nameof(CompletedAt))]
    public DateTime? CompletedAt { get; set; }

    [Column(nameof(DurationMs))]
    public long? DurationMs { get; set; }

    [Column(nameof(Message))]
    public string? Message { get; set; }

    [Column(nameof(Error))]
    public string? Error { get; set; }
}

[TableName(BackgroundJobRunLogDto.TableName)]
[PrimaryKey(nameof(BackgroundJobRunLogDto.Id), AutoIncrement = false)]
[ExplicitColumns]
internal class BackgroundJobRunLogDto
{
    public const string TableName = "JobsJobsJobsBackgroundJobRunLog";

    [Column(nameof(Id))]
    public Guid Id { get; set; }

    [Column(nameof(RunId))]
    public Guid RunId { get; set; }

    [Column(nameof(Level))]
    public string Level { get; set; } = string.Empty;

    [Column(nameof(Message))]
    public string Message { get; set; } = string.Empty;

    [Column(nameof(LoggedAt))]
    public DateTime LoggedAt { get; set; }
}
