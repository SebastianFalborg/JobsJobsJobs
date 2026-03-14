using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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

    public bool TryMarkRunning(IRecurringBackgroundJob job);

    public void MarkRunning(IRecurringBackgroundJob job);

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

internal sealed class BackgroundJobDashboardStateStore : IBackgroundJobDashboardStateStore
{
    private readonly ConcurrentDictionary<string, BackgroundJobDashboardItem> _items;
    private readonly IReadOnlyDictionary<string, BackgroundJobDashboardItem> _definitions;

    public BackgroundJobDashboardStateStore(IEnumerable<IRecurringBackgroundJob> jobs)
    {
        _definitions = jobs
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
        if (_items.TryGetValue(alias, out BackgroundJobDashboardItem? existing))
        {
            item = existing;
            return true;
        }

        item = null!;
        return false;
    }

    public bool TryMarkRunning(IRecurringBackgroundJob job)
    {
        var alias = BackgroundJobDashboardNaming.GetAlias(job);

        while (true)
        {
            if (_items.TryGetValue(alias, out BackgroundJobDashboardItem? existing))
            {
                if (existing.IsRunning)
                {
                    return false;
                }

                var updated = Clone(existing);
                updated.IsRunning = true;
                updated.LastStartedAt = DateTime.UtcNow;
                updated.LastStatus = BackgroundJobStatus.Running;
                updated.LastMessage = null;
                updated.LastError = null;

                if (_items.TryUpdate(alias, updated, existing))
                {
                    return true;
                }

                continue;
            }

            var item = _definitions.TryGetValue(alias, out BackgroundJobDashboardItem? definition)
                ? Clone(definition)
                : CreateDefinition(job);
            item.IsRunning = true;
            item.LastStartedAt = DateTime.UtcNow;
            item.LastStatus = BackgroundJobStatus.Running;
            item.LastMessage = null;
            item.LastError = null;

            if (_items.TryAdd(alias, item))
            {
                return true;
            }
        }
    }

    public void MarkRunning(IRecurringBackgroundJob job)
        => Update(job, item =>
        {
            item.IsRunning = true;
            item.LastStartedAt = DateTime.UtcNow;
            item.LastStatus = BackgroundJobStatus.Running;
            item.LastMessage = null;
            item.LastError = null;
        });

    public void MarkCompleted(IRecurringBackgroundJob job, BackgroundJobStatus status, EventMessages messages, IDictionary<string, object?>? state = null)
        => Update(job, item =>
        {
            item.IsRunning = false;
            item.LastCompletedAt = DateTime.UtcNow;
            item.LastDuration = item.LastStartedAt.HasValue ? item.LastCompletedAt - item.LastStartedAt.Value : null;
            item.LastStatus = status;
            item.LastMessage = ResolveMessage(messages, state);
            if (status == BackgroundJobStatus.Succeeded)
            {
                item.LastSucceededAt = item.LastCompletedAt;
                item.LastError = null;
            }
        });

    public void MarkFailed(IRecurringBackgroundJob job, EventMessages messages, IDictionary<string, object?>? state = null)
        => Update(job, item =>
        {
            item.IsRunning = false;
            item.LastCompletedAt = DateTime.UtcNow;
            item.LastDuration = item.LastStartedAt.HasValue ? item.LastCompletedAt - item.LastStartedAt.Value : null;
            item.LastFailedAt = item.LastCompletedAt;
            item.LastStatus = BackgroundJobStatus.Failed;
            item.LastMessage = ResolveMessage(messages, state);
            item.LastError = ResolveError(messages, state);
        });

    private void Update(IRecurringBackgroundJob job, Action<BackgroundJobDashboardItem> update)
    {
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

    private static BackgroundJobDashboardItem CreateDefinition(IRecurringBackgroundJob job)
        => new()
        {
            Alias = BackgroundJobDashboardNaming.GetAlias(job),
            Name = BackgroundJobDashboardNaming.GetDisplayName(job),
            Type = job.GetType().FullName ?? job.GetType().Name,
            Delay = job.Delay,
            Period = job.Period,
            ServerRoles = job.ServerRoles,
            AllowManualTrigger = true,
            LastStatus = BackgroundJobStatus.Idle,
        };

    private static BackgroundJobDashboardItem Clone(BackgroundJobDashboardItem item)
        => new()
        {
            Alias = item.Alias,
            Name = item.Name,
            Type = item.Type,
            Period = item.Period,
            Delay = item.Delay,
            ServerRoles = item.ServerRoles,
            AllowManualTrigger = item.AllowManualTrigger,
            IsRunning = item.IsRunning,
            LastStartedAt = item.LastStartedAt,
            LastCompletedAt = item.LastCompletedAt,
            LastDuration = item.LastDuration,
            LastSucceededAt = item.LastSucceededAt,
            LastFailedAt = item.LastFailedAt,
            LastStatus = item.LastStatus,
            LastError = item.LastError,
            LastMessage = item.LastMessage,
        };

    private static string? ResolveMessage(EventMessages messages, IDictionary<string, object?>? state)
    {
        if (state is not null && state.TryGetValue(BackgroundJobDashboardStateKeys.Message, out object? stateMessage) && stateMessage is string message)
        {
            return message;
        }

        return messages.GetAll().Select(x => x.Message).FirstOrDefault(x => string.IsNullOrWhiteSpace(x) is false);
    }

    private static string? ResolveError(EventMessages messages, IDictionary<string, object?>? state)
    {
        if (state is not null && state.TryGetValue(BackgroundJobDashboardStateKeys.ErrorMessage, out object? stateError) && stateError is string error)
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
        BackgroundJobDashboardItem[] items = _stateStore.GetAll().Select(item => new BackgroundJobDashboardItem
        {
            Alias = item.Alias,
            Name = item.Name,
            Type = item.Type,
            Period = item.Period,
            Delay = item.Delay,
            ServerRoles = item.ServerRoles,
            AllowManualTrigger = item.AllowManualTrigger,
            IsRunning = item.IsRunning,
            LastStartedAt = item.LastStartedAt,
            LastCompletedAt = item.LastCompletedAt,
            LastDuration = item.LastDuration,
            LastSucceededAt = item.LastSucceededAt,
            LastFailedAt = item.LastFailedAt,
            LastStatus = item.LastStatus,
            LastError = item.LastError,
            LastMessage = item.LastMessage,
            LatestRun = item.LatestRun,
        }).ToArray();
        IReadOnlyDictionary<string, BackgroundJobRunHistoryItem> latestRuns = _runHistoryService.GetLatestRuns(items.Select(x => x.Alias));

        foreach (BackgroundJobDashboardItem item in items)
        {
            if (latestRuns.TryGetValue(item.Alias, out BackgroundJobRunHistoryItem? latestRun))
            {
                item.LatestRun = latestRun;
            }
        }

        return items;
    }
}

internal sealed class BackgroundJobManualTriggerDispatcher : IBackgroundJobManualTriggerDispatcher
{
    private readonly IReadOnlyDictionary<string, IRecurringBackgroundJob> _jobs;
    private readonly ILogger<BackgroundJobManualTriggerDispatcher> _logger;
    private readonly IMainDom _mainDom;
    private readonly IBackgroundJobRunRecorder _runRecorder;
    private readonly IRuntimeState _runtimeState;
    private readonly IServerRoleAccessor _serverRoleAccessor;
    private readonly IBackgroundJobDashboardStateStore _stateStore;

    public BackgroundJobManualTriggerDispatcher(
        IEnumerable<IRecurringBackgroundJob> jobs,
        IBackgroundJobDashboardStateStore stateStore,
        IBackgroundJobRunRecorder runRecorder,
        IRuntimeState runtimeState,
        IMainDom mainDom,
        IServerRoleAccessor serverRoleAccessor,
        ILogger<BackgroundJobManualTriggerDispatcher> logger)
    {
        _stateStore = stateStore;
        _runRecorder = runRecorder;
        _runtimeState = runtimeState;
        _mainDom = mainDom;
        _serverRoleAccessor = serverRoleAccessor;
        _logger = logger;
        _jobs = jobs.ToDictionary(BackgroundJobDashboardNaming.GetAlias, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<BackgroundJobTriggerResult> TriggerAsync(string alias)
    {
        if (_jobs.TryGetValue(alias, out IRecurringBackgroundJob? job) is false)
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

        if (_stateStore.TryMarkRunning(job) is false)
        {
            return new BackgroundJobTriggerResult
            {
                Status = BackgroundJobTriggerOperationStatus.AlreadyRunning,
                Message = $"The background job '{alias}' is already running.",
            };
        }

        var messages = new EventMessages();
        _runRecorder.MarkStarted(job, BackgroundJobRunTrigger.Manual);
        _runRecorder.WriteLog(alias, BackgroundJobRunLogLevel.Information, "Manually triggered");

        try
        {
            await job.RunJobAsync();
            messages.Add(new EventMessage("Background job", "The job completed successfully.", EventMessageType.Success));
            _stateStore.MarkCompleted(
                job,
                BackgroundJobStatus.Succeeded,
                messages,
                new Dictionary<string, object?> { [BackgroundJobDashboardStateKeys.Message] = "Manual run completed successfully." });
            _runRecorder.MarkCompleted(
                job,
                BackgroundJobStatus.Succeeded,
                messages,
                new Dictionary<string, object?> { [BackgroundJobDashboardStateKeys.Message] = "Manual run completed successfully." });

            return new BackgroundJobTriggerResult
            {
                Status = BackgroundJobTriggerOperationStatus.Success,
                Message = "The job completed successfully.",
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while manually running recurring background job {JobAlias}", alias);
            messages.Add(new EventMessage("Background job", ex.Message, EventMessageType.Error));
            _stateStore.MarkFailed(
                job,
                messages,
                new Dictionary<string, object?>
                {
                    [BackgroundJobDashboardStateKeys.ErrorMessage] = ex.Message,
                    [BackgroundJobDashboardStateKeys.Message] = "Manual run failed.",
                });
            _runRecorder.MarkFailed(
                job,
                messages,
                new Dictionary<string, object?>
                {
                    [BackgroundJobDashboardStateKeys.ErrorMessage] = ex.Message,
                    [BackgroundJobDashboardStateKeys.Message] = "Manual run failed.",
                });

            return new BackgroundJobTriggerResult
            {
                Status = BackgroundJobTriggerOperationStatus.Failed,
                Message = ex.Message,
            };
        }
    }

    private static BackgroundJobTriggerResult NotAllowed(string message)
        => new()
        {
            Status = BackgroundJobTriggerOperationStatus.NotAllowed,
            Message = message,
        };
}
