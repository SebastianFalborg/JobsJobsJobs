using JobsJobsJobs.Core.BackgroundJobs;
using JobsJobsJobs.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Infrastructure.Notifications;

namespace JobsJobsJobs.Infrastructure.DependencyInjection;

public static class JobsJobsJobsBuilderExtensions
{
    public const string BackgroundJobDashboardConfigurationSection = "BackgroundJobDashboard";

    public static IUmbracoBuilder AddJobsJobsJobsCore(this IUmbracoBuilder builder)
    {
        builder.Services.Configure<BackgroundJobDashboardOptions>(builder.Config.GetSection(BackgroundJobDashboardConfigurationSection));
        builder.Services.AddSingleton<IBackgroundJobRunExecutionContextAccessor, BackgroundJobRunExecutionContextAccessor>();
        builder.Services.AddSingleton<IBackgroundJobStopCoordinator, BackgroundJobStopCoordinator>();
        builder.Services.AddSingleton<IBackgroundJobCronScheduler, BackgroundJobCronScheduler>();
        builder.Services.AddSingleton<IBackgroundJobCronSuppressionCoordinator, BackgroundJobCronSuppressionCoordinator>();
        builder.Services.AddSingleton<IBackgroundJobDashboardStateStore, BackgroundJobDashboardStateStore>();
        builder.Services.AddSingleton<IBackgroundJobDashboardService, BackgroundJobDashboardService>();
        builder.Services.AddTransient<IBackgroundJobExecutionCancellation, BackgroundJobExecutionCancellation>();
        builder.Services.AddTransient(typeof(IBackgroundJobRunLogWriter<>), typeof(BackgroundJobRunLogWriter<>));
        return builder;
    }

    public static IUmbracoBuilder AddJobsJobsJobsInfrastructure(this IUmbracoBuilder builder)
    {
        builder.Services.AddSingleton<IBackgroundJobManualTriggerDispatcher, BackgroundJobManualTriggerDispatcher>();
        builder.Services.AddSingleton<IBackgroundJobStopDispatcher, BackgroundJobStopDispatcher>();
        builder.Services.AddSingleton<BackgroundJobRunStore>();
        builder.Services.AddSingleton<IBackgroundJobRunHistoryService>(x => x.GetRequiredService<BackgroundJobRunStore>());
        builder.Services.AddSingleton<IBackgroundJobRunRecorder>(x => x.GetRequiredService<BackgroundJobRunStore>());
        builder.Services.AddSingleton<IBackgroundJobRunRetentionService, BackgroundJobRunRetentionService>();

        builder.AddNotificationAsyncHandler<RecurringBackgroundJobExecutingNotification, BackgroundJobDashboardNotificationHandler>();
        builder.AddNotificationAsyncHandler<RecurringBackgroundJobExecutedNotification, BackgroundJobDashboardNotificationHandler>();
        builder.AddNotificationAsyncHandler<RecurringBackgroundJobFailedNotification, BackgroundJobDashboardNotificationHandler>();
        builder.AddNotificationAsyncHandler<RecurringBackgroundJobIgnoredNotification, BackgroundJobDashboardNotificationHandler>();
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartingNotification, BackgroundJobRunMigrationHandler>();
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, BackgroundJobRunMigrationHandler>();

        builder.AddRecurringBackgroundJob<BackgroundJobRunHistoryCleanupJob>();

        return builder;
    }
}
