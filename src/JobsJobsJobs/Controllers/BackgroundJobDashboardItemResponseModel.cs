using System;
using System.Collections.Generic;

namespace JobsJobsJobs.Controllers;

public record BackgroundJobDashboardItemResponseModel
{
    public string Alias { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Type { get; init; } = string.Empty;

    public TimeSpan Period { get; init; }

    public TimeSpan Delay { get; init; }

    public bool UsesCronSchedule { get; init; }

    public string? ScheduleDisplay { get; init; }

    public string? CronExpression { get; init; }

    public string? TimeZoneId { get; init; }

    public IEnumerable<string> ServerRoles { get; init; } = Array.Empty<string>();

    public bool AllowManualTrigger { get; init; }

    public bool CanStop { get; init; }

    public bool IsRunning { get; init; }

    public bool StopRequested { get; init; }

    public DateTime? LastStartedAt { get; init; }

    public DateTime? LastCompletedAt { get; init; }

    public TimeSpan? LastDuration { get; init; }

    public DateTime? LastSucceededAt { get; init; }

    public DateTime? LastFailedAt { get; init; }

    public string LastStatus { get; init; } = string.Empty;

    public string? LastError { get; init; }

    public string? LastMessage { get; init; }

    public BackgroundJobDashboardRunResponseModel? LatestRun { get; init; }

    public IEnumerable<BackgroundJobDashboardRunResponseModel> RecentRuns { get; init; } = Array.Empty<BackgroundJobDashboardRunResponseModel>();
}

public record BackgroundJobDashboardRunResponseModel
{
    public Guid Id { get; init; }

    public string Trigger { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public DateTime StartedAt { get; init; }

    public DateTime? CompletedAt { get; init; }

    public TimeSpan? Duration { get; init; }

    public string? Message { get; init; }

    public string? Error { get; init; }

    public IEnumerable<BackgroundJobDashboardRunLogResponseModel> Logs { get; init; } = Array.Empty<BackgroundJobDashboardRunLogResponseModel>();
}

public record BackgroundJobDashboardRunLogResponseModel
{
    public DateTime LoggedAt { get; init; }

    public string Level { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}

public record BackgroundJobDashboardCollectionResponseModel
{
    public int Total { get; init; }

    public IEnumerable<BackgroundJobDashboardItemResponseModel> Items { get; init; } = Array.Empty<BackgroundJobDashboardItemResponseModel>();
}
