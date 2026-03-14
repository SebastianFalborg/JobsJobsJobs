using System.Threading;
using System.Threading.Tasks;
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

    public BackgroundJobDashboardNotificationHandler(
        IBackgroundJobDashboardStateStore stateStore,
        IBackgroundJobRunRecorder runRecorder,
        IBackgroundJobRunExecutionContextAccessor runExecutionContextAccessor)
    {
        _stateStore = stateStore;
        _runRecorder = runRecorder;
        _runExecutionContextAccessor = runExecutionContextAccessor;
    }

    public Task HandleAsync(RecurringBackgroundJobExecutingNotification notification, CancellationToken cancellationToken)
    {
        _runExecutionContextAccessor.Set(_runExecutionContextAccessor.Create(notification.Job, BackgroundJobRunTrigger.Automatic));
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
            _runExecutionContextAccessor.Clear();
            throw;
        }

        return Task.CompletedTask;
    }

    public Task HandleAsync(RecurringBackgroundJobExecutedNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            _stateStore.MarkCompleted(notification.Job, BackgroundJobStatus.Succeeded, notification.Messages, notification.State);
            _runRecorder.MarkCompleted(notification.Job, BackgroundJobStatus.Succeeded, notification.Messages, notification.State);
        }
        finally
        {
            _runExecutionContextAccessor.Clear();
        }

        return Task.CompletedTask;
    }

    public Task HandleAsync(RecurringBackgroundJobFailedNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            _stateStore.MarkFailed(notification.Job, notification.Messages, notification.State);
            _runRecorder.MarkFailed(notification.Job, notification.Messages, notification.State);
        }
        finally
        {
            _runExecutionContextAccessor.Clear();
        }

        return Task.CompletedTask;
    }

    public Task HandleAsync(RecurringBackgroundJobIgnoredNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            _stateStore.MarkCompleted(notification.Job, BackgroundJobStatus.Ignored, notification.Messages, notification.State);
            _runRecorder.MarkCompleted(notification.Job, BackgroundJobStatus.Ignored, notification.Messages, notification.State);
        }
        finally
        {
            _runExecutionContextAccessor.Clear();
        }

        return Task.CompletedTask;
    }
}
