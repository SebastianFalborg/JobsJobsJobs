using System;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace JobsJobsJobs.BackgroundJobs;

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

public class BackgroundJobDashboardItem
{
    public string Alias { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public TimeSpan Period { get; set; }

    public TimeSpan Delay { get; set; }

    public ServerRole[] ServerRoles { get; set; } = Array.Empty<ServerRole>();

    public bool AllowManualTrigger { get; set; } = true;

    public bool IsRunning { get; set; }

    public DateTime? LastStartedAt { get; set; }

    public DateTime? LastCompletedAt { get; set; }

    public TimeSpan? LastDuration { get; set; }

    public DateTime? LastSucceededAt { get; set; }

    public DateTime? LastFailedAt { get; set; }

    public BackgroundJobStatus LastStatus { get; set; } = BackgroundJobStatus.Idle;

    public string? LastError { get; set; }

    public string? LastMessage { get; set; }

    public BackgroundJobRunHistoryItem? LatestRun { get; set; }
}

public class BackgroundJobTriggerResult
{
    public string? Message { get; init; }

    public BackgroundJobTriggerOperationStatus Status { get; init; }
}

internal static class BackgroundJobDashboardNaming
{
    internal static string GetAlias(IRecurringBackgroundJob job) => GetAlias(job.GetType());

    internal static string GetAlias(Type jobType) => jobType.FullName ?? jobType.Name;

    internal static string GetDisplayName(IRecurringBackgroundJob job) => GetDisplayName(job.GetType());

    internal static string GetDisplayName(Type jobType)
    {
        var name = jobType.Name;
        return name.EndsWith("Job", StringComparison.Ordinal) ? name[..^3] : name;
    }
}
