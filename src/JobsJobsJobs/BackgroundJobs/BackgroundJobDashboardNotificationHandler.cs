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
    private readonly IBackgroundJobDashboardStateStore _stateStore;
    private readonly IBackgroundJobRunRecorder _runRecorder;

    public BackgroundJobDashboardNotificationHandler(IBackgroundJobDashboardStateStore stateStore, IBackgroundJobRunRecorder runRecorder)
    {
        _stateStore = stateStore;
        _runRecorder = runRecorder;
    }

    public Task HandleAsync(RecurringBackgroundJobExecutingNotification notification, CancellationToken cancellationToken)
    {
        _stateStore.MarkRunning(notification.Job);
        _runRecorder.MarkStarted(notification.Job, BackgroundJobRunTrigger.Automatic);
        return Task.CompletedTask;
    }

    public Task HandleAsync(RecurringBackgroundJobExecutedNotification notification, CancellationToken cancellationToken)
    {
        _stateStore.MarkCompleted(notification.Job, BackgroundJobStatus.Succeeded, notification.Messages, notification.State);
        _runRecorder.MarkCompleted(notification.Job, BackgroundJobStatus.Succeeded, notification.Messages, notification.State);
        return Task.CompletedTask;
    }

    public Task HandleAsync(RecurringBackgroundJobFailedNotification notification, CancellationToken cancellationToken)
    {
        _stateStore.MarkFailed(notification.Job, notification.Messages, notification.State);
        _runRecorder.MarkFailed(notification.Job, notification.Messages, notification.State);
        return Task.CompletedTask;
    }

    public Task HandleAsync(RecurringBackgroundJobIgnoredNotification notification, CancellationToken cancellationToken)
    {
        _stateStore.MarkCompleted(notification.Job, BackgroundJobStatus.Ignored, notification.Messages, notification.State);
        _runRecorder.MarkCompleted(notification.Job, BackgroundJobStatus.Ignored, notification.Messages, notification.State);
        return Task.CompletedTask;
    }
}
