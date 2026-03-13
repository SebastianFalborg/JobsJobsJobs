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

    public IEnumerable<string> ServerRoles { get; set; } = Array.Empty<string>();

    public bool AllowManualTrigger { get; set; }

    public bool IsRunning { get; set; }

    public DateTime? LastStartedAt { get; set; }

    public DateTime? LastCompletedAt { get; set; }

    public DateTime? LastSucceededAt { get; set; }

    public DateTime? LastFailedAt { get; set; }

    public string LastStatus { get; set; } = string.Empty;

    public string? LastError { get; set; }

    public string? LastMessage { get; set; }
}

public class BackgroundJobDashboardCollectionResponseModel
{
    public int Total { get; set; }

    public IEnumerable<BackgroundJobDashboardItemResponseModel> Items { get; set; } = Array.Empty<BackgroundJobDashboardItemResponseModel>();
}
