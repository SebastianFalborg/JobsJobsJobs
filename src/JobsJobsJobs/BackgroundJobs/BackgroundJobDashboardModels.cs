using System;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace JobsJobsJobs.BackgroundJobs;

public class BackgroundJobDashboardOptions
{
    public bool IncludeUmbracoJobs { get; set; }
}

public static class BackgroundJobDashboardStateKeys
{
    public const string ErrorMessage = "backgroundJobErrorMessage";
    public const string Message = "backgroundJobMessage";
}

public enum BackgroundJobStatus
{
    Idle,
    Running,
    Succeeded,
    Failed,
    Stopped,
    Ignored,
}

public enum BackgroundJobTriggerOperationStatus
{
    Success,
    NotFound,
    AlreadyRunning,
    NotAllowed,
    Failed,
}

public enum BackgroundJobStopOperationStatus
{
    Success,
    NotFound,
    NotRunning,
    NotSupported,
    AlreadyRequested,
}

public class BackgroundJobDashboardItem
{
    public string Alias { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public TimeSpan Period { get; set; }

    public TimeSpan Delay { get; set; }

    public bool UsesCronSchedule { get; set; }

    public string? ScheduleDisplay { get; set; }

    public string? CronExpression { get; set; }

    public string? TimeZoneId { get; set; }

    public ServerRole[] ServerRoles { get; set; } = Array.Empty<ServerRole>();

    public bool AllowManualTrigger { get; set; } = true;

    public bool CanStop { get; set; }

    public bool IsRunning { get; set; }

    public bool StopRequested { get; set; }

    public DateTime? LastStartedAt { get; set; }

    public DateTime? LastCompletedAt { get; set; }

    public TimeSpan? LastDuration { get; set; }

    public DateTime? LastSucceededAt { get; set; }

    public DateTime? LastFailedAt { get; set; }

    public BackgroundJobStatus LastStatus { get; set; } = BackgroundJobStatus.Idle;

    public string? LastError { get; set; }

    public string? LastMessage { get; set; }

    public BackgroundJobRunHistoryItem? LatestRun { get; set; }

    public IReadOnlyCollection<BackgroundJobRunHistoryItem> RecentRuns { get; set; } = Array.Empty<BackgroundJobRunHistoryItem>();
}

public class BackgroundJobTriggerResult
{
    public string? Message { get; init; }

    public BackgroundJobTriggerOperationStatus Status { get; init; }
}

public class BackgroundJobStopResult
{
    public string? Message { get; init; }

    public BackgroundJobStopOperationStatus Status { get; init; }
}

internal static class BackgroundJobDashboardNaming
{
    internal static string GetAlias(IRecurringBackgroundJob job) => GetAlias(job.GetType());

    internal static string GetAlias(Type jobType) => GetJobType(jobType).FullName ?? GetJobType(jobType).Name;

    internal static bool IsUmbracoJob(IRecurringBackgroundJob job) => IsUmbracoJob(job.GetType());

    internal static bool IsUmbracoJob(Type jobType)
        => GetJobType(jobType).Namespace?.StartsWith("Umbraco.", StringComparison.Ordinal) is true;

    internal static bool ShouldInclude(IRecurringBackgroundJob job, BackgroundJobDashboardOptions options)
        => options.IncludeUmbracoJobs || IsUmbracoJob(job) is false;

    internal static bool ShouldInclude(string alias, BackgroundJobDashboardOptions options)
        => options.IncludeUmbracoJobs || alias.StartsWith("Umbraco.", StringComparison.Ordinal) is false;

    internal static bool SupportsStop(IRecurringBackgroundJob job) => job is IStoppableRecurringBackgroundJob;

    internal static string GetDisplayName(IRecurringBackgroundJob job) => GetDisplayName(job.GetType());

    internal static string GetDisplayName(Type jobType)
    {
        var name = GetJobType(jobType).Name;
        return name.EndsWith("Job", StringComparison.Ordinal) ? name[..^3] : name;
    }

    private static Type GetJobType(Type jobType)
    {
        if (jobType.IsGenericType is false)
        {
            return jobType;
        }

        Type genericTypeDefinition = jobType.GetGenericTypeDefinition();
        if (genericTypeDefinition == typeof(CronRecurringBackgroundJobAdapter<>)
            || genericTypeDefinition == typeof(StoppableCronRecurringBackgroundJobAdapter<>))
        {
            return jobType.GetGenericArguments()[0];
        }

        return jobType;
    }
}
