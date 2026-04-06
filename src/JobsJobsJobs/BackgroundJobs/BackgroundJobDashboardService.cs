using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Runtime;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace JobsJobsJobs.BackgroundJobs;

public interface IBackgroundJobDashboardStateStore
{
    public IReadOnlyCollection<BackgroundJobDashboardItem> GetAll();

    public bool TryGet(string alias, out BackgroundJobDashboardItem item);

    public bool TryBeginExecution(IRecurringBackgroundJob job);

    public void BeginExecution(IRecurringBackgroundJob job);

    public void AbortExecution(IRecurringBackgroundJob job);

    public void MarkRunning(IRecurringBackgroundJob job);

    public void MarkStopRequested(IRecurringBackgroundJob job);

    public void MarkCompleted(IRecurringBackgroundJob job, BackgroundJobStatus status, EventMessages messages, IDictionary<string, object?>? state = null);

    public void MarkFailed(IRecurringBackgroundJob job, EventMessages messages, IDictionary<string, object?>? state = null);
}

public interface IBackgroundJobDashboardService
{
    public IReadOnlyCollection<BackgroundJobDashboardItem> GetJobs();
}

public interface IBackgroundJobManualTriggerDispatcher
{
    public Task<BackgroundJobTriggerResult> TriggerAsync(string alias);
}

public interface IBackgroundJobStopDispatcher
{
    public BackgroundJobStopResult RequestStop(string alias);
}

internal sealed class BackgroundJobDashboardStateStore : IBackgroundJobDashboardStateStore
{
    private readonly ConcurrentDictionary<string, int> _activeExecutionCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, BackgroundJobDashboardItem> _items;
    private readonly IReadOnlyDictionary<string, BackgroundJobDashboardItem> _definitions;
    private readonly BackgroundJobDashboardOptions _options;
    private readonly IBackgroundJobRunExecutionContextAccessor _runExecutionContextAccessor;

    public BackgroundJobDashboardStateStore(
        IEnumerable<IRecurringBackgroundJob> jobs,
        IBackgroundJobRunExecutionContextAccessor runExecutionContextAccessor,
        IOptions<BackgroundJobDashboardOptions> options)
    {
        _options = options.Value;
        _runExecutionContextAccessor = runExecutionContextAccessor;
        _definitions = jobs
            .Where(ShouldInclude)
            .Select(CreateDefinition)
            .ToDictionary(item => item.Alias, StringComparer.OrdinalIgnoreCase);

        _items = new ConcurrentDictionary<string, BackgroundJobDashboardItem>(
            _definitions.ToDictionary(
                x => x.Key,
                x => x.Value,
                StringComparer.OrdinalIgnoreCase));
    }

    public IReadOnlyCollection<BackgroundJobDashboardItem> GetAll() => _items.Values.OrderBy(x => x.Name).ToArray();

    public bool TryGet(string alias, out BackgroundJobDashboardItem item)
    {
        if (_items.TryGetValue(alias, out var existing))
        {
            item = existing;
            return true;
        }

        item = null!;
        return false;
    }

    public bool TryBeginExecution(IRecurringBackgroundJob job)
    {
        if (ShouldInclude(job) is false)
        {
            return false;
        }

        var alias = BackgroundJobDashboardNaming.GetAlias(job);
        return TryIncrementExecutionCount(alias, onlyIfZero: true);
    }

    public void BeginExecution(IRecurringBackgroundJob job)
    {
        if (ShouldInclude(job) is false)
        {
            return;
        }

        var alias = BackgroundJobDashboardNaming.GetAlias(job);
        IncrementExecutionCount(alias);
    }

    public void AbortExecution(IRecurringBackgroundJob job)
    {
        if (ShouldInclude(job) is false)
        {
            return;
        }

        var alias = BackgroundJobDashboardNaming.GetAlias(job);
        var remainingExecutions = DecrementExecutionCount(alias);

        Update(job, item =>
        {
            item.IsRunning = remainingExecutions > 0;
            item.StopRequested = remainingExecutions > 0 && item.StopRequested;

            if (remainingExecutions == 0 && item.LastStatus == BackgroundJobStatus.Running)
            {
                item.LastStatus = BackgroundJobStatus.Idle;
                item.LastMessage = null;
                item.LastError = null;
                item.StopRequested = false;
            }
        });
    }

    public void MarkRunning(IRecurringBackgroundJob job)
    {
        if (ShouldInclude(job) is false)
        {
            return;
        }

        var startedAt = _runExecutionContextAccessor.Get(job)?.StartedAt ?? DateTime.UtcNow;

        Update(job, item =>
        {
            item.IsRunning = true;
            item.StopRequested = false;
            item.LastStartedAt = startedAt;
            item.LastStatus = BackgroundJobStatus.Running;
            item.LastMessage = null;
            item.LastError = null;
        });
    }

    public void MarkStopRequested(IRecurringBackgroundJob job)
    {
        if (ShouldInclude(job) is false)
        {
            return;
        }

        Update(job, item =>
        {
            item.StopRequested = true;
            item.LastMessage = "Stop requested.";
            item.LastError = null;
        });
    }

    public void MarkCompleted(IRecurringBackgroundJob job, BackgroundJobStatus status, EventMessages messages, IDictionary<string, object?>? state = null)
    {
        if (ShouldInclude(job) is false)
        {
            return;
        }

        var completedAt = DateTime.UtcNow;
        var startedAt = _runExecutionContextAccessor.Get(job)?.StartedAt;
        var remainingExecutions = DecrementExecutionCount(BackgroundJobDashboardNaming.GetAlias(job));

        Update(job, item =>
        {
            item.IsRunning = remainingExecutions > 0;
            item.StopRequested = remainingExecutions > 0 && item.StopRequested;
            item.LastCompletedAt = completedAt;
            item.LastDuration = startedAt.HasValue
                ? completedAt - startedAt.Value
                : item.LastStartedAt.HasValue ? completedAt - item.LastStartedAt.Value : null;
            item.LastStatus = status;
            item.LastMessage = ResolveMessage(messages, state);
            item.LastError = status == BackgroundJobStatus.Failed ? item.LastError : null;
            if (status == BackgroundJobStatus.Succeeded)
            {
                item.LastSucceededAt = item.LastCompletedAt;
                item.LastError = null;
            }
            else if (status == BackgroundJobStatus.Stopped)
            {
                item.LastError = null;
            }
        });
    }

    public void MarkFailed(IRecurringBackgroundJob job, EventMessages messages, IDictionary<string, object?>? state = null)
    {
        if (ShouldInclude(job) is false)
        {
            return;
        }

        var completedAt = DateTime.UtcNow;
        var startedAt = _runExecutionContextAccessor.Get(job)?.StartedAt;
        var remainingExecutions = DecrementExecutionCount(BackgroundJobDashboardNaming.GetAlias(job));

        Update(job, item =>
        {
            item.IsRunning = remainingExecutions > 0;
            item.StopRequested = remainingExecutions > 0 && item.StopRequested;
            item.LastCompletedAt = completedAt;
            item.LastDuration = startedAt.HasValue
                ? completedAt - startedAt.Value
                : item.LastStartedAt.HasValue ? completedAt - item.LastStartedAt.Value : null;
            item.LastFailedAt = item.LastCompletedAt;
            item.LastStatus = BackgroundJobStatus.Failed;
            item.LastMessage = ResolveMessage(messages, state);
            item.LastError = ResolveError(messages, state);
        });
    }

    private void Update(IRecurringBackgroundJob job, Action<BackgroundJobDashboardItem> update)
    {
        if (ShouldInclude(job) is false)
        {
            return;
        }

        var alias = BackgroundJobDashboardNaming.GetAlias(job);
        _items.AddOrUpdate(
            alias,
            _ =>
            {
                var item = _definitions.TryGetValue(alias, out BackgroundJobDashboardItem? definition)
                    ? Clone(definition)
                    : CreateDefinition(job);
                update(item);
                return item;
            },
            (_, existing) =>
            {
                update(existing);
                return existing;
            });
    }

    private bool ShouldInclude(IRecurringBackgroundJob job) => BackgroundJobDashboardNaming.ShouldInclude(job, _options);

    private void IncrementExecutionCount(string alias)
        => _activeExecutionCounts.AddOrUpdate(alias, 1, (_, existing) => existing + 1);

    private bool TryIncrementExecutionCount(string alias, bool onlyIfZero)
    {
        while (true)
        {
            if (_activeExecutionCounts.TryGetValue(alias, out var existingCount))
            {
                if (onlyIfZero && existingCount > 0)
                {
                    return false;
                }

                if (_activeExecutionCounts.TryUpdate(alias, existingCount + 1, existingCount))
                {
                    return true;
                }

                continue;
            }

            if (_activeExecutionCounts.TryAdd(alias, 1))
            {
                return true;
            }
        }
    }

    private int DecrementExecutionCount(string alias)
    {
        while (true)
        {
            if (_activeExecutionCounts.TryGetValue(alias, out var existingCount) is false)
            {
                return 0;
            }

            if (existingCount <= 1)
            {
                if (_activeExecutionCounts.TryRemove(alias, out _))
                {
                    return 0;
                }

                continue;
            }

            if (_activeExecutionCounts.TryUpdate(alias, existingCount - 1, existingCount))
            {
                return existingCount - 1;
            }
        }
    }

    private static BackgroundJobDashboardItem CreateDefinition(IRecurringBackgroundJob job)
    {
        var metadata = job as IBackgroundJobDashboardMetadata;

        return new BackgroundJobDashboardItem
        {
            Alias = BackgroundJobDashboardNaming.GetAlias(job),
            Name = BackgroundJobDashboardNaming.GetDisplayName(job),
            Type = metadata?.JobType.FullName ?? job.GetType().FullName ?? job.GetType().Name,
            Delay = job.Delay,
            Period = job.Period,
            UsesCronSchedule = metadata?.UsesCronSchedule ?? false,
            ScheduleDisplay = metadata?.ScheduleDisplay ?? job.Period.ToString(),
            CronExpression = metadata?.CronExpression,
            TimeZoneId = metadata?.TimeZoneId,
            ServerRoles = job.ServerRoles,
            AllowManualTrigger = true,
            CanStop = BackgroundJobDashboardNaming.SupportsStop(job),
            LastStatus = BackgroundJobStatus.Idle,
        };
    }

    private static BackgroundJobDashboardItem Clone(BackgroundJobDashboardItem item)
        => new()
        {
            Alias = item.Alias,
            Name = item.Name,
            Type = item.Type,
            Period = item.Period,
            Delay = item.Delay,
            UsesCronSchedule = item.UsesCronSchedule,
            ScheduleDisplay = item.ScheduleDisplay,
            CronExpression = item.CronExpression,
            TimeZoneId = item.TimeZoneId,
            ServerRoles = item.ServerRoles,
            AllowManualTrigger = item.AllowManualTrigger,
            CanStop = item.CanStop,
            IsRunning = item.IsRunning,
            StopRequested = item.StopRequested,
            LastStartedAt = item.LastStartedAt,
            LastCompletedAt = item.LastCompletedAt,
            LastDuration = item.LastDuration,
            LastSucceededAt = item.LastSucceededAt,
            LastFailedAt = item.LastFailedAt,
            LastStatus = item.LastStatus,
            LastError = item.LastError,
            LastMessage = item.LastMessage,
            LatestRun = item.LatestRun,
            RecentRuns = item.RecentRuns,
        };

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

internal sealed class BackgroundJobDashboardService : IBackgroundJobDashboardService
{
    private readonly IBackgroundJobDashboardStateStore _stateStore;
    private readonly IBackgroundJobRunHistoryService _runHistoryService;

    public BackgroundJobDashboardService(IBackgroundJobDashboardStateStore stateStore, IBackgroundJobRunHistoryService runHistoryService)
    {
        _stateStore = stateStore;
        _runHistoryService = runHistoryService;
    }

    public IReadOnlyCollection<BackgroundJobDashboardItem> GetJobs()
    {
        var items = _stateStore.GetAll().Select(item => new BackgroundJobDashboardItem
        {
            Alias = item.Alias,
            Name = item.Name,
            Type = item.Type,
            Period = item.Period,
            Delay = item.Delay,
            UsesCronSchedule = item.UsesCronSchedule,
            ScheduleDisplay = item.ScheduleDisplay,
            CronExpression = item.CronExpression,
            TimeZoneId = item.TimeZoneId,
            ServerRoles = item.ServerRoles,
            AllowManualTrigger = item.AllowManualTrigger,
            CanStop = item.CanStop,
            IsRunning = item.IsRunning,
            StopRequested = item.StopRequested,
            LastStartedAt = item.LastStartedAt,
            LastCompletedAt = item.LastCompletedAt,
            LastDuration = item.LastDuration,
            LastSucceededAt = item.LastSucceededAt,
            LastFailedAt = item.LastFailedAt,
            LastStatus = item.LastStatus,
            LastError = item.LastError,
            LastMessage = item.LastMessage,
            LatestRun = item.LatestRun,
            RecentRuns = item.RecentRuns,
        }).ToArray();
        var latestRuns = _runHistoryService.GetLatestRuns(items.Select(x => x.Alias));
        var recentRuns = _runHistoryService.GetRecentRuns(items.Select(x => x.Alias));

        foreach (var item in items)
        {
            if (latestRuns.TryGetValue(item.Alias, out var latestRun))
            {
                item.LatestRun = latestRun;
                HydrateSummaryFromLatestRun(item, latestRun);
            }

            if (recentRuns.TryGetValue(item.Alias, out var recentJobRuns))
            {
                item.RecentRuns = recentJobRuns;
            }
        }

        return items;
    }

    private static void HydrateSummaryFromLatestRun(BackgroundJobDashboardItem item, BackgroundJobRunHistoryItem latestRun)
    {
        if (HasInMemorySummary(item) || item.IsRunning)
        {
            return;
        }

        item.LastStartedAt ??= latestRun.StartedAt;
        item.LastCompletedAt ??= latestRun.CompletedAt;
        item.LastDuration ??= latestRun.Duration;

        if (string.IsNullOrWhiteSpace(item.LastMessage))
        {
            item.LastMessage = latestRun.Message;
        }

        if (string.IsNullOrWhiteSpace(item.LastError))
        {
            item.LastError = latestRun.Error;
        }

        switch (latestRun.Status)
        {
            case BackgroundJobStatus.Succeeded:
                item.LastStatus = BackgroundJobStatus.Succeeded;
                item.LastSucceededAt ??= latestRun.CompletedAt;
                break;
            case BackgroundJobStatus.Failed:
                item.LastStatus = BackgroundJobStatus.Failed;
                item.LastFailedAt ??= latestRun.CompletedAt;
                break;
            case BackgroundJobStatus.Stopped:
                item.LastStatus = BackgroundJobStatus.Stopped;
                break;
            case BackgroundJobStatus.Ignored:
                item.LastStatus = BackgroundJobStatus.Ignored;
                break;
        }
    }

    private static bool HasInMemorySummary(BackgroundJobDashboardItem item)
        => item.LastStartedAt.HasValue
            || item.LastCompletedAt.HasValue
            || item.LastDuration.HasValue
            || item.LastSucceededAt.HasValue
            || item.LastFailedAt.HasValue
            || item.LastStatus != BackgroundJobStatus.Idle
            || string.IsNullOrWhiteSpace(item.LastMessage) is false
            || string.IsNullOrWhiteSpace(item.LastError) is false;
}

internal sealed class BackgroundJobManualTriggerDispatcher : IBackgroundJobManualTriggerDispatcher
{
    private readonly IReadOnlyDictionary<string, IRecurringBackgroundJob> _jobs;
    private readonly BackgroundJobDashboardOptions _options;
    private readonly ILogger<BackgroundJobManualTriggerDispatcher> _logger;
    private readonly IMainDom _mainDom;
    private readonly IBackgroundJobRunRecorder _runRecorder;
    private readonly IBackgroundJobStopCoordinator _stopCoordinator;
    private readonly IBackgroundJobRunExecutionContextAccessor _runExecutionContextAccessor;
    private readonly IRuntimeState _runtimeState;
    private readonly IServerRoleAccessor _serverRoleAccessor;
    private readonly IBackgroundJobDashboardStateStore _stateStore;

    public BackgroundJobManualTriggerDispatcher(
        IEnumerable<IRecurringBackgroundJob> jobs,
        IBackgroundJobDashboardStateStore stateStore,
        IBackgroundJobRunRecorder runRecorder,
        IBackgroundJobStopCoordinator stopCoordinator,
        IBackgroundJobRunExecutionContextAccessor runExecutionContextAccessor,
        IOptions<BackgroundJobDashboardOptions> options,
        IRuntimeState runtimeState,
        IMainDom mainDom,
        IServerRoleAccessor serverRoleAccessor,
        ILogger<BackgroundJobManualTriggerDispatcher> logger)
    {
        _stateStore = stateStore;
        _runRecorder = runRecorder;
        _stopCoordinator = stopCoordinator;
        _runExecutionContextAccessor = runExecutionContextAccessor;
        _options = options.Value;
        _runtimeState = runtimeState;
        _mainDom = mainDom;
        _serverRoleAccessor = serverRoleAccessor;
        _logger = logger;
        _jobs = jobs
            .Where(job => BackgroundJobDashboardNaming.ShouldInclude(job, _options))
            .ToDictionary(BackgroundJobDashboardNaming.GetAlias, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<BackgroundJobTriggerResult> TriggerAsync(string alias)
    {
        if (_jobs.TryGetValue(alias, out var job) is false)
        {
            return new BackgroundJobTriggerResult
            {
                Status = BackgroundJobTriggerOperationStatus.NotFound,
                Message = $"No recurring background job found for alias '{alias}'.",
            };
        }

        if (_runtimeState.Level != RuntimeLevel.Run)
        {
            return NotAllowed("The application is not in the Run runtime state.");
        }

        if (job.ServerRoles.Contains(_serverRoleAccessor.CurrentServerRole) is false)
        {
            return NotAllowed($"The current server role '{_serverRoleAccessor.CurrentServerRole}' is not allowed to run this job.");
        }

        if (_mainDom.IsMainDom is false)
        {
            return NotAllowed("The current server is not MainDom.");
        }

        BackgroundJobRunExecutionContext context = _runExecutionContextAccessor.Create(job, BackgroundJobRunTrigger.Manual);
        _runExecutionContextAccessor.Set(job, context);
        _stopCoordinator.Register(job, context);

        if (_stateStore.TryBeginExecution(job) is false)
        {
            _stopCoordinator.Complete(context.RunId);
            _runExecutionContextAccessor.Clear(job);
            return new BackgroundJobTriggerResult
            {
                Status = BackgroundJobTriggerOperationStatus.AlreadyRunning,
                Message = $"The background job '{alias}' is already running.",
            };
        }

        var messages = new EventMessages();
        var runPersisted = false;
        var runningStateMarked = false;

        try
        {
            _runRecorder.MarkStarted(job, BackgroundJobRunTrigger.Manual);
            runPersisted = true;
            _stateStore.MarkRunning(job);
            runningStateMarked = true;
            _runRecorder.WriteLog(job, BackgroundJobRunLogLevel.Information, "Manually triggered");
            await job.RunJobAsync();
            var completionStatus = _stopCoordinator.IsStopRequested(context.RunId)
                ? BackgroundJobStatus.Stopped
                : BackgroundJobStatus.Succeeded;
            var completionMessage = completionStatus == BackgroundJobStatus.Stopped
                ? "Manual run stopped."
                : "Manual run completed successfully.";
            messages.Add(new EventMessage("Background job", completionMessage, EventMessageType.Success));
            _stateStore.MarkCompleted(
                job,
                completionStatus,
                messages,
                new Dictionary<string, object?> { [BackgroundJobDashboardStateKeys.Message] = completionMessage });
            _runRecorder.MarkCompleted(
                job,
                completionStatus,
                messages,
                new Dictionary<string, object?> { [BackgroundJobDashboardStateKeys.Message] = completionMessage });

            return new BackgroundJobTriggerResult
            {
                Status = BackgroundJobTriggerOperationStatus.Success,
                Message = completionStatus == BackgroundJobStatus.Stopped ? "The job stopped." : "The job completed successfully.",
            };
        }
        catch (Exception ex)
        {
            if (_stopCoordinator.IsStopRequested(context.RunId))
            {
                messages.Add(new EventMessage("Background job", "Manual run stopped.", EventMessageType.Warning));

                if (runPersisted is false)
                {
                    _stateStore.AbortExecution(job);
                }
                else
                {
                    _stateStore.MarkCompleted(
                        job,
                        BackgroundJobStatus.Stopped,
                        messages,
                        new Dictionary<string, object?> { [BackgroundJobDashboardStateKeys.Message] = "Manual run stopped." });
                    _runRecorder.MarkCompleted(
                        job,
                        BackgroundJobStatus.Stopped,
                        messages,
                        new Dictionary<string, object?> { [BackgroundJobDashboardStateKeys.Message] = "Manual run stopped." });
                }

                return new BackgroundJobTriggerResult
                {
                    Status = BackgroundJobTriggerOperationStatus.Success,
                    Message = "The job stopped.",
                };
            }

            _logger.LogError(ex, "Unhandled exception while manually running recurring background job {JobAlias}", alias);
            messages.Add(new EventMessage("Background job", ex.Message, EventMessageType.Error));

            if (runPersisted is false)
            {
                _stateStore.AbortExecution(job);
            }
            else
            {
                if (runningStateMarked)
                {
                    _stateStore.MarkFailed(
                        job,
                        messages,
                        new Dictionary<string, object?>
                        {
                            [BackgroundJobDashboardStateKeys.ErrorMessage] = ex.Message,
                            [BackgroundJobDashboardStateKeys.Message] = "Manual run failed.",
                        });
                }
                else
                {
                    _stateStore.AbortExecution(job);
                }

                _runRecorder.MarkFailed(
                    job,
                    messages,
                    new Dictionary<string, object?>
                    {
                        [BackgroundJobDashboardStateKeys.ErrorMessage] = ex.Message,
                        [BackgroundJobDashboardStateKeys.Message] = "Manual run failed.",
                    });
            }

            return new BackgroundJobTriggerResult
            {
                Status = BackgroundJobTriggerOperationStatus.Failed,
                Message = ex.Message,
            };
        }
        finally
        {
            _stopCoordinator.Complete(context.RunId);
            _runExecutionContextAccessor.Clear(job);
        }
    }

    private static BackgroundJobTriggerResult NotAllowed(string message)
        => new()
        {
            Status = BackgroundJobTriggerOperationStatus.NotAllowed,
            Message = message,
        };
}

internal sealed class BackgroundJobStopDispatcher : IBackgroundJobStopDispatcher
{
    private readonly IReadOnlyDictionary<string, IRecurringBackgroundJob> _jobs;
    private readonly BackgroundJobDashboardOptions _options;
    private readonly IBackgroundJobRunRecorder _runRecorder;
    private readonly IBackgroundJobDashboardStateStore _stateStore;
    private readonly IBackgroundJobStopCoordinator _stopCoordinator;

    public BackgroundJobStopDispatcher(
        IEnumerable<IRecurringBackgroundJob> jobs,
        IBackgroundJobDashboardStateStore stateStore,
        IBackgroundJobRunRecorder runRecorder,
        IBackgroundJobStopCoordinator stopCoordinator,
        IOptions<BackgroundJobDashboardOptions> options)
    {
        _stateStore = stateStore;
        _runRecorder = runRecorder;
        _stopCoordinator = stopCoordinator;
        _options = options.Value;
        _jobs = jobs
            .Where(job => BackgroundJobDashboardNaming.ShouldInclude(job, _options))
            .ToDictionary(BackgroundJobDashboardNaming.GetAlias, StringComparer.OrdinalIgnoreCase);
    }

    public BackgroundJobStopResult RequestStop(string alias)
    {
        if (_jobs.TryGetValue(alias, out IRecurringBackgroundJob? job) is false)
        {
            return new BackgroundJobStopResult
            {
                Status = BackgroundJobStopOperationStatus.NotFound,
                Message = $"No recurring background job found for alias '{alias}'.",
            };
        }

        if (_stateStore.TryGet(alias, out BackgroundJobDashboardItem? item) is false || item.IsRunning is false)
        {
            return new BackgroundJobStopResult
            {
                Status = BackgroundJobStopOperationStatus.NotRunning,
                Message = $"The background job '{alias}' is not currently running.",
            };
        }

        if (item.CanStop is false)
        {
            return new BackgroundJobStopResult
            {
                Status = BackgroundJobStopOperationStatus.NotSupported,
                Message = $"The background job '{alias}' does not support stopping.",
            };
        }

        BackgroundJobStopRequestState result = _stopCoordinator.RequestStop(alias);

        switch (result)
        {
            case BackgroundJobStopRequestState.Success:
                _stateStore.MarkStopRequested(job);
                _runRecorder.WriteLog(job, BackgroundJobRunLogLevel.Warning, "Stop requested.");
                return new BackgroundJobStopResult
                {
                    Status = BackgroundJobStopOperationStatus.Success,
                    Message = "Stop requested.",
                };
            case BackgroundJobStopRequestState.AlreadyRequested:
                return new BackgroundJobStopResult
                {
                    Status = BackgroundJobStopOperationStatus.AlreadyRequested,
                    Message = "Stop has already been requested for this job.",
                };
            case BackgroundJobStopRequestState.NotSupported:
                return new BackgroundJobStopResult
                {
                    Status = BackgroundJobStopOperationStatus.NotSupported,
                    Message = $"The background job '{alias}' does not support stopping.",
                };
            default:
                return new BackgroundJobStopResult
                {
                    Status = BackgroundJobStopOperationStatus.NotRunning,
                    Message = $"The background job '{alias}' is not currently running.",
                };
        }
    }
}
