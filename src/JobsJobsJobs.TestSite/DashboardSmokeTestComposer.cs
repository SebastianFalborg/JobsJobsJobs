using Microsoft.Extensions.DependencyInjection;
using JobsJobsJobs.BackgroundJobs;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Extensions;

namespace JobsJobsJobs.TestSite;

internal sealed class DashboardSmokeTestComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddRecurringBackgroundJob<DashboardSmokeTestJob>();
        builder.Services.AddRecurringBackgroundJob<DashboardFileWriteStopTestJob>();
        builder.Services.AddCronBackgroundJob<Sunday2200UtcCronTestJob>();
        builder.Services.AddCronBackgroundJob<Sunday2230UtcCronTestJob>();
    }
}
