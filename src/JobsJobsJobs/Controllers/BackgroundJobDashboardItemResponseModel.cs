using System;
using System.Collections.Generic;

namespace JobsJobsJobs.Controllers;

public class BackgroundJobDashboardItemResponseModel
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

    public IEnumerable<string> ServerRoles { get; set; } = Array.Empty<string>();

    public bool AllowManualTrigger { get; set; }

    public bool CanStop { get; set; }

    public bool IsRunning { get; set; }

    public bool StopRequested { get; set; }

    public DateTime? LastStartedAt { get; set; }

    public DateTime? LastCompletedAt { get; set; }

    public TimeSpan? LastDuration { get; set; }

    public DateTime? LastSucceededAt { get; set; }

    public DateTime? LastFailedAt { get; set; }

    public string LastStatus { get; set; } = string.Empty;

    public string? LastError { get; set; }

    public string? LastMessage { get; set; }

    public BackgroundJobDashboardRunResponseModel? LatestRun { get; set; }
}

public class BackgroundJobDashboardRunResponseModel
{
    public Guid Id { get; set; }

    public string Trigger { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public TimeSpan? Duration { get; set; }

    public string? Message { get; set; }

    public string? Error { get; set; }

    public IEnumerable<BackgroundJobDashboardRunLogResponseModel> Logs { get; set; } = Array.Empty<BackgroundJobDashboardRunLogResponseModel>();
}

public class BackgroundJobDashboardRunLogResponseModel
{
    public DateTime LoggedAt { get; set; }

    public string Level { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}

public class BackgroundJobDashboardCollectionResponseModel
{
    public int Total { get; set; }

    public IEnumerable<BackgroundJobDashboardItemResponseModel> Items { get; set; } = Array.Empty<BackgroundJobDashboardItemResponseModel>();
}
