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

    public BackgroundJobsController(
        IBackgroundJobDashboardService backgroundJobDashboardService,
        IBackgroundJobManualTriggerDispatcher backgroundJobManualTriggerDispatcher)
    {
        _backgroundJobDashboardService = backgroundJobDashboardService;
        _backgroundJobManualTriggerDispatcher = backgroundJobManualTriggerDispatcher;
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
                ServerRoles = x.ServerRoles.Select(role => role.ToString()),
                AllowManualTrigger = x.AllowManualTrigger,
                IsRunning = x.IsRunning,
                LastStartedAt = x.LastStartedAt,
                LastCompletedAt = x.LastCompletedAt,
                LastSucceededAt = x.LastSucceededAt,
                LastFailedAt = x.LastFailedAt,
                LastStatus = x.LastStatus.ToString(),
                LastError = x.LastError,
                LastMessage = x.LastMessage,
            })
            .ToArray();

        return Ok(new BackgroundJobDashboardCollectionResponseModel
        {
            Items = items,
            Total = items.Length,
        });
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

    private static ProblemDetails CreateProblemDetails(int statusCode, string? detail)
        => new()
        {
            Title = "Background job execution failed",
            Detail = detail,
            Status = statusCode,
            Type = "Error",
        };
}
