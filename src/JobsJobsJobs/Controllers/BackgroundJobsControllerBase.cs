using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Cms.Web.Common.Routing;

namespace JobsJobsJobs.Controllers;

[ApiController]
[BackOfficeRoute("jobsjobsjobs/api/v{version:apiVersion}/background-jobs")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
[MapToApi(Constants.ApiName)]
public abstract class BackgroundJobsControllerBase : ControllerBase
{
}
