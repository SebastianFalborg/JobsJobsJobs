using System.Threading;
using System.Threading.Tasks;
using JobsJobsJobs.Core.BackgroundJobs;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Infrastructure.Notifications;

namespace JobsJobsJobs.BackgroundJobs;

internal sealed class BackgroundJobDashboardNotificationHandler :
    INotificationAsyncHandler<RecurringBackgroundJobExecutingNotification>,
    INotificationAsyncHandler<RecurringBackgroundJobExecutedNotification>,
    INotificationAsyncHandler<RecurringBackgroundJobFailedNotification>,
    INotificationAsyncHandler<RecurringBackgroundJobIgnoredNotification>
{
    private readonly IBackgroundJobRunExecutionContextAccessor _runExecutionContextAccessor;
    private readonly IBackgroundJobDashboardStateStore _stateStore;
    private readonly IBackgroundJobRunRecorder _runRecorder;
    private readonly IBackgroundJobStopCoordinator _stopCoordinator;
    private readonly IBackgroundJobCronSuppressionCoordinator _cronSuppressionCoordinator;

    public BackgroundJobDashboardNotificationHandler(
        IBackgroundJobDashboardStateStore stateStore,
        IBackgroundJobRunRecorder runRecorder,
        IBackgroundJobRunExecutionContextAccessor runExecutionContextAccessor,
        IBackgroundJobStopCoordinator stopCoordinator,
        IBackgroundJobCronSuppressionCoordinator cronSuppressionCoordinator)
    {
        _stateStore = stateStore;
        _runRecorder = runRecorder;
        _runExecutionContextAccessor = runExecutionContextAccessor;
        _stopCoordinator = stopCoordinator;
        _cronSuppressionCoordinator = cronSuppressionCoordinator;
    }

    public Task HandleAsync(RecurringBackgroundJobExecutingNotification notification, CancellationToken cancellationToken)
    {
        BackgroundJobRunExecutionContext context = _runExecutionContextAccessor.Create(notification.Job, BackgroundJobRunTrigger.Automatic);

        if (notification.Job is ICronRecurringBackgroundJobAdapter cronJob
            && cronJob.ShouldExecute(context) is false)
        {
            _cronSuppressionCoordinator.Suppress(context.JobAlias);
            context.ShouldExecute = false;
            _runExecutionContextAccessor.Set(notification.Job, context);
            return Task.CompletedTask;
        }

        _runExecutionContextAccessor.Set(notification.Job, context);
        _stopCoordinator.Register(notification.Job, context);
        _stateStore.BeginExecution(notification.Job);
        var runPersisted = false;

        try
        {
            _runRecorder.MarkStarted(notification.Job, BackgroundJobRunTrigger.Automatic);
            runPersisted = true;
            _stateStore.MarkRunning(notification.Job);
        }
        catch (System.Exception ex)
        {
            if (runPersisted)
            {
                var messages = new EventMessages();
                messages.Add(new EventMessage("Background job", ex.Message, EventMessageType.Error));
                _runRecorder.MarkFailed(
                    notification.Job,
                    messages,
                    new System.Collections.Generic.Dictionary<string, object?>
                    {
                        [BackgroundJobDashboardStateKeys.ErrorMessage] = ex.Message,
                        [BackgroundJobDashboardStateKeys.Message] = "Automatic run failed during startup.",
                    });
            }

            _stateStore.AbortExecution(notification.Job);
            _stopCoordinator.Complete(context.RunId);
            _runExecutionContextAccessor.Clear(notification.Job);
            throw;
        }

        return Task.CompletedTask;
    }

    public Task HandleAsync(RecurringBackgroundJobExecutedNotification notification, CancellationToken cancellationToken)
    {
        BackgroundJobRunExecutionContext? context = _runExecutionContextAccessor.Get(notification.Job);
        if (_cronSuppressionCoordinator.TryConsumeNotificationSkip(BackgroundJobDashboardNaming.GetAlias(notification.Job)))
        {
            _runExecutionContextAccessor.Clear(notification.Job);
            return Task.CompletedTask;
        }

        if (context is not null && context.ShouldExecute is false)
        {
            _runExecutionContextAccessor.Clear(notification.Job);
            return Task.CompletedTask;
        }

        try
        {
            var status = context is not null && _stopCoordinator.IsStopRequested(context.RunId)
                ? BackgroundJobStatus.Stopped
                : BackgroundJobStatus.Succeeded;
            _stateStore.MarkCompleted(notification.Job, status, notification.Messages, notification.State);
            _runRecorder.MarkCompleted(notification.Job, status, notification.Messages, notification.State);
        }
        finally
        {
            if (context is not null)
            {
                _stopCoordinator.Complete(context.RunId);
            }

            _runExecutionContextAccessor.Clear(notification.Job);
        }

        return Task.CompletedTask;
    }

    public Task HandleAsync(RecurringBackgroundJobFailedNotification notification, CancellationToken cancellationToken)
    {
        BackgroundJobRunExecutionContext? context = _runExecutionContextAccessor.Get(notification.Job);
        if (_cronSuppressionCoordinator.TryConsumeNotificationSkip(BackgroundJobDashboardNaming.GetAlias(notification.Job)))
        {
            _runExecutionContextAccessor.Clear(notification.Job);
            return Task.CompletedTask;
        }

        if (context is not null && context.ShouldExecute is false)
        {
            _runExecutionContextAccessor.Clear(notification.Job);
            return Task.CompletedTask;
        }

        try
        {
            if (context is not null && _stopCoordinator.IsStopRequested(context.RunId))
            {
                _stateStore.MarkCompleted(notification.Job, BackgroundJobStatus.Stopped, notification.Messages, notification.State);
                _runRecorder.MarkCompleted(notification.Job, BackgroundJobStatus.Stopped, notification.Messages, notification.State);
            }
            else
            {
                _stateStore.MarkFailed(notification.Job, notification.Messages, notification.State);
                _runRecorder.MarkFailed(notification.Job, notification.Messages, notification.State);
            }
        }
        finally
        {
            if (context is not null)
            {
                _stopCoordinator.Complete(context.RunId);
            }

            _runExecutionContextAccessor.Clear(notification.Job);
        }

        return Task.CompletedTask;
    }

    public Task HandleAsync(RecurringBackgroundJobIgnoredNotification notification, CancellationToken cancellationToken)
    {
        BackgroundJobRunExecutionContext? context = _runExecutionContextAccessor.Get(notification.Job);
        if (_cronSuppressionCoordinator.TryConsumeNotificationSkip(BackgroundJobDashboardNaming.GetAlias(notification.Job)))
        {
            _runExecutionContextAccessor.Clear(notification.Job);
            return Task.CompletedTask;
        }

        if (context is not null && context.ShouldExecute is false)
        {
            _runExecutionContextAccessor.Clear(notification.Job);
            return Task.CompletedTask;
        }

        try
        {
            _stateStore.MarkCompleted(notification.Job, BackgroundJobStatus.Ignored, notification.Messages, notification.State);
            _runRecorder.MarkCompleted(notification.Job, BackgroundJobStatus.Ignored, notification.Messages, notification.State);
        }
        finally
        {
            if (context is not null)
            {
                _stopCoordinator.Complete(context.RunId);
            }

            _runExecutionContextAccessor.Clear(notification.Job);
        }

        return Task.CompletedTask;
    }
}
