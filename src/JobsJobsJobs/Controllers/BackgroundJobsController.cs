using System.Linq;
using System.Threading.Tasks;
using Asp.Versioning;
using JobsJobsJobs.BackgroundJobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace JobsJobsJobs.Controllers;

[ApiVersion("1.0")]
[ApiExplorerSettings(GroupName = "JobsJobsJobs")]
public class BackgroundJobsController : BackgroundJobsControllerBase
{
    private readonly IBackgroundJobDashboardService _backgroundJobDashboardService;
    private readonly IBackgroundJobManualTriggerDispatcher _backgroundJobManualTriggerDispatcher;
    private readonly IBackgroundJobStopDispatcher _backgroundJobStopDispatcher;

    public BackgroundJobsController(
        IBackgroundJobDashboardService backgroundJobDashboardService,
        IBackgroundJobManualTriggerDispatcher backgroundJobManualTriggerDispatcher,
        IBackgroundJobStopDispatcher backgroundJobStopDispatcher
    )
    {
        _backgroundJobDashboardService = backgroundJobDashboardService;
        _backgroundJobManualTriggerDispatcher = backgroundJobManualTriggerDispatcher;
        _backgroundJobStopDispatcher = backgroundJobStopDispatcher;
    }

    [HttpGet]
    [ProducesResponseType(typeof(BackgroundJobDashboardCollectionResponseModel), StatusCodes.Status200OK)]
    public ActionResult<BackgroundJobDashboardCollectionResponseModel> Get()
    {
        var items = _backgroundJobDashboardService
            .GetJobs()
            .Select(x => new BackgroundJobDashboardItemResponseModel
            {
                Alias = x.Alias,
                Name = x.Name,
                Type = x.Type,
                Period = x.Period,
                Delay = x.Delay,
                UsesCronSchedule = x.UsesCronSchedule,
                ScheduleDisplay = x.ScheduleDisplay,
                CronExpression = x.CronExpression,
                TimeZoneId = x.TimeZoneId,
                ServerRoles = x.ServerRoles.Select(role => role.ToString()),
                AllowManualTrigger = x.AllowManualTrigger,
                CanStop = x.CanStop,
                IsRunning = x.IsRunning,
                StopRequested = x.StopRequested,
                LastStartedAt = x.LastStartedAt,
                LastCompletedAt = x.LastCompletedAt,
                LastDuration = x.LastDuration,
                LastSucceededAt = x.LastSucceededAt,
                LastFailedAt = x.LastFailedAt,
                LastStatus = x.LastStatus.ToString(),
                LastError = x.LastError,
                LastMessage = x.LastMessage,
                LatestRun = x.LatestRun is null
                    ? null
                    : new BackgroundJobDashboardRunResponseModel
                    {
                        Id = x.LatestRun.Id,
                        Trigger = x.LatestRun.Trigger,
                        Status = x.LatestRun.Status.ToString(),
                        StartedAt = x.LatestRun.StartedAt,
                        CompletedAt = x.LatestRun.CompletedAt,
                        Duration = x.LatestRun.Duration,
                        Message = x.LatestRun.Message,
                        Error = x.LatestRun.Error,
                        Logs = x.LatestRun.Logs.Select(log => new BackgroundJobDashboardRunLogResponseModel
                        {
                            LoggedAt = log.LoggedAt,
                            Level = log.Level,
                            Message = log.Message,
                        }),
                    },
                RecentRuns = x.RecentRuns.Select(run => new BackgroundJobDashboardRunResponseModel
                {
                    Id = run.Id,
                    Trigger = run.Trigger,
                    Status = run.Status.ToString(),
                    StartedAt = run.StartedAt,
                    CompletedAt = run.CompletedAt,
                    Duration = run.Duration,
                    Message = run.Message,
                    Error = run.Error,
                    Logs = run.Logs.Select(log => new BackgroundJobDashboardRunLogResponseModel
                    {
                        LoggedAt = log.LoggedAt,
                        Level = log.Level,
                        Message = log.Message,
                    }),
                }),
            })
            .ToArray();

        return Ok(new BackgroundJobDashboardCollectionResponseModel { Items = items, Total = items.Length });
    }

    [HttpPost("run/{alias}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Run(string alias)
    {
        BackgroundJobTriggerResult result = await _backgroundJobManualTriggerDispatcher.TriggerAsync(alias);

        return result.Status switch
        {
            BackgroundJobTriggerOperationStatus.Success => Ok(),
            BackgroundJobTriggerOperationStatus.NotFound => NotFound(CreateProblemDetails(StatusCodes.Status404NotFound, result.Message)),
            BackgroundJobTriggerOperationStatus.AlreadyRunning => Conflict(CreateProblemDetails(StatusCodes.Status409Conflict, result.Message)),
            BackgroundJobTriggerOperationStatus.NotAllowed => Conflict(CreateProblemDetails(StatusCodes.Status409Conflict, result.Message)),
            _ => StatusCode(StatusCodes.Status500InternalServerError, CreateProblemDetails(StatusCodes.Status500InternalServerError, result.Message)),
        };
    }

    [HttpPost("stop/{alias}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public IActionResult Stop(string alias)
    {
        BackgroundJobStopResult result = _backgroundJobStopDispatcher.RequestStop(alias);

        return result.Status switch
        {
            BackgroundJobStopOperationStatus.Success => Ok(),
            BackgroundJobStopOperationStatus.NotFound => NotFound(CreateProblemDetails(StatusCodes.Status404NotFound, result.Message)),
            BackgroundJobStopOperationStatus.NotRunning => Conflict(CreateProblemDetails(StatusCodes.Status409Conflict, result.Message)),
            BackgroundJobStopOperationStatus.NotSupported => Conflict(CreateProblemDetails(StatusCodes.Status409Conflict, result.Message)),
            BackgroundJobStopOperationStatus.AlreadyRequested => Conflict(CreateProblemDetails(StatusCodes.Status409Conflict, result.Message)),
            _ => StatusCode(StatusCodes.Status500InternalServerError, CreateProblemDetails(StatusCodes.Status500InternalServerError, result.Message)),
        };
    }

    private static ProblemDetails CreateProblemDetails(int statusCode, string? detail) =>
        new()
        {
            Title = "Background job execution failed",
            Detail = detail,
            Status = statusCode,
            Type = "Error",
        };
}
