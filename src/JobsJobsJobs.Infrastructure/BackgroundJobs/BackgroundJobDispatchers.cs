using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JobsJobsJobs.Core.BackgroundJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Runtime;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace JobsJobsJobs.Infrastructure.BackgroundJobs;

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
        ILogger<BackgroundJobManualTriggerDispatcher> logger
    )
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
        _jobs = jobs.Where(job => BackgroundJobDashboardNaming.ShouldInclude(job, _options))
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
            var completionStatus = _stopCoordinator.IsStopRequested(context.RunId) ? BackgroundJobStatus.Stopped : BackgroundJobStatus.Succeeded;
            var completionMessage = completionStatus == BackgroundJobStatus.Stopped ? "Manual run stopped." : "Manual run completed successfully.";
            messages.Add(new EventMessage("Background job", completionMessage, EventMessageType.Success));
            _stateStore.MarkCompleted(
                job,
                completionStatus,
                messages,
                new Dictionary<string, object?> { [BackgroundJobDashboardStateKeys.Message] = completionMessage }
            );
            _runRecorder.MarkCompleted(
                job,
                completionStatus,
                messages,
                new Dictionary<string, object?> { [BackgroundJobDashboardStateKeys.Message] = completionMessage }
            );

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
                        new Dictionary<string, object?> { [BackgroundJobDashboardStateKeys.Message] = "Manual run stopped." }
                    );
                    _runRecorder.MarkCompleted(
                        job,
                        BackgroundJobStatus.Stopped,
                        messages,
                        new Dictionary<string, object?> { [BackgroundJobDashboardStateKeys.Message] = "Manual run stopped." }
                    );
                }

                return new BackgroundJobTriggerResult { Status = BackgroundJobTriggerOperationStatus.Success, Message = "The job stopped." };
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
                        }
                    );
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
                    }
                );
            }

            return new BackgroundJobTriggerResult { Status = BackgroundJobTriggerOperationStatus.Failed, Message = ex.Message };
        }
        finally
        {
            _stopCoordinator.Complete(context.RunId);
            _runExecutionContextAccessor.Clear(job);
        }
    }

    private static BackgroundJobTriggerResult NotAllowed(string message) =>
        new() { Status = BackgroundJobTriggerOperationStatus.NotAllowed, Message = message };
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
        IOptions<BackgroundJobDashboardOptions> options
    )
    {
        _stateStore = stateStore;
        _runRecorder = runRecorder;
        _stopCoordinator = stopCoordinator;
        _options = options.Value;
        _jobs = jobs.Where(job => BackgroundJobDashboardNaming.ShouldInclude(job, _options))
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
                return new BackgroundJobStopResult { Status = BackgroundJobStopOperationStatus.Success, Message = "Stop requested." };
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
