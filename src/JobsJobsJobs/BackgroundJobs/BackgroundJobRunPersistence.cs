using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NPoco;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Umbraco.Cms.Infrastructure.Scoping;

namespace JobsJobsJobs.BackgroundJobs;

internal sealed class BackgroundJobRunStore : IBackgroundJobRunHistoryService, IBackgroundJobRunRecorder
{
    private static readonly TimeSpan[] _writeRetryDelays = { TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(500) };
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
        if (ShouldInclude(job) is false)
        {
            return;
        }

        var context = _runExecutionContextAccessor.Current ?? _runExecutionContextAccessor.Create(job, trigger);
        var alias = BackgroundJobDashboardNaming.GetAlias(job);
        LogRunStarted(alias, trigger);
        var run = new BackgroundJobRunDto
        {
            Id = context.RunId,
            JobAlias = alias,
            JobName = BackgroundJobDashboardNaming.GetDisplayName(job),
            Trigger = context.Trigger.ToString(),
            Status = BackgroundJobStatus.Running.ToString(),
            StartedAt = context.StartedAt,
            CompletedAt = null,
            DurationMs = 0,
            Message = string.Empty,
            Error = string.Empty,
        };

        TryExecuteWrite(alias, "persist background job run start", () =>
        {
            using var scope = _scopeProvider.CreateScope(autoComplete: true);
            scope.Database.Insert(run);
        });
    }

    public void MarkCompleted(IRecurringBackgroundJob job, BackgroundJobStatus status, EventMessages messages, IDictionary<string, object?>? state = null)
    {
        var alias = BackgroundJobDashboardNaming.GetAlias(job);
        var context = _runExecutionContextAccessor.Current;
        if (ShouldInclude(alias) is false)
        {
            return;
        }

        var completedAt = DateTime.UtcNow;
        var message = ResolveMessage(messages, state);
        var error = status == BackgroundJobStatus.Failed ? ResolveError(messages, state) : null;
        LogRunCompleted(alias, status, message, error);

        TryExecuteWrite(alias, "persist background job run completion", () =>
        {
            using var scope = _scopeProvider.CreateScope(autoComplete: true);
            var run = context is not null
                ? scope.Database.SingleOrDefaultById<BackgroundJobRunDto>(context.RunId)
                : GetLatestRunningRun(scope.Database, alias);
            if (run is null)
            {
                return;
            }

            run.Status = status.ToString();
            run.CompletedAt = completedAt;
            run.DurationMs = (long)Math.Max(0, (completedAt - run.StartedAt).TotalMilliseconds);
            run.Message = message ?? string.Empty;
            run.Error = error ?? string.Empty;
            scope.Database.Update(run);

            if (string.IsNullOrWhiteSpace(message) is false)
            {
                InsertLog(scope.Database, run.Id, BackgroundJobRunLogLevel.Information, message!);
            }

            if (string.IsNullOrWhiteSpace(error) is false)
            {
                InsertLog(scope.Database, run.Id, BackgroundJobRunLogLevel.Error, error!);
            }
        });
    }

    public void MarkFailed(IRecurringBackgroundJob job, EventMessages messages, IDictionary<string, object?>? state = null)
        => MarkCompleted(job, BackgroundJobStatus.Failed, messages, state);

    public void WriteLog(string alias, BackgroundJobRunLogLevel level, string message)
    {
        var context = _runExecutionContextAccessor.Current;
        if (string.IsNullOrWhiteSpace(message) || ShouldInclude(alias) is false)
        {
            return;
        }

        WriteApplicationLog(alias, level, message);

        TryExecuteWrite(alias, "persist background job run log", () =>
        {
            using var scope = _scopeProvider.CreateScope(autoComplete: true);
            var runId = context?.RunId ?? GetLatestRunningRun(scope.Database, alias)?.Id;
            if (runId is null)
            {
                return;
            }

            InsertLog(scope.Database, runId.Value, level, message);
        });
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

    private static void InsertLog(IDatabase database, Guid runId, BackgroundJobRunLogLevel level, string message)
        => database.Insert(new BackgroundJobRunLogDto
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            Level = level.ToString(),
            Message = message,
            LoggedAt = DateTime.UtcNow,
        });

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

        for (var attempt = 0; attempt <= _writeRetryDelays.Length; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (attempt == _writeRetryDelays.Length)
                {
                    break;
                }

                Thread.Sleep(_writeRetryDelays[attempt]);
            }
        }

        _logger.LogWarning(lastException, "Failed to {Operation} for background job {JobAlias} after {AttemptCount} attempts. Continuing without persisted history update.", operation, alias, _writeRetryDelays.Length + 1);
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

internal sealed class BackgroundJobRunLogWriter<TJob> : IBackgroundJobRunLogWriter<TJob>
{
    private readonly IBackgroundJobRunRecorder _runRecorder;
    private readonly string _alias = BackgroundJobDashboardNaming.GetAlias(typeof(TJob));

    public BackgroundJobRunLogWriter(IBackgroundJobRunRecorder runRecorder) => _runRecorder = runRecorder;

    public void Information(string message) => _runRecorder.WriteLog(_alias, BackgroundJobRunLogLevel.Information, message);

    public void Warning(string message) => _runRecorder.WriteLog(_alias, BackgroundJobRunLogLevel.Warning, message);

    public void Error(string message) => _runRecorder.WriteLog(_alias, BackgroundJobRunLogLevel.Error, message);
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
