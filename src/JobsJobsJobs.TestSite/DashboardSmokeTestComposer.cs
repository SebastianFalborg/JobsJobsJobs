using JobsJobsJobs.BackgroundJobs;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace JobsJobsJobs.TestSite;

internal sealed class DashboardSmokeTestComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddRecurringBackgroundJob<DashboardSmokeTestJob>();
        builder.AddRecurringBackgroundJob<DashboardFileWriteStopTestJob>();
        builder.AddRecurringBackgroundJob<DashboardHeavyResourceStressJob>();
        builder.AddRecurringBackgroundJob<DashboardLogStormTestJob>();
        builder.AddCronBackgroundJob<EveryTwoMinutesLongRunningOverlapTestJob>();
        builder.AddCronBackgroundJob<Sunday2200UtcCronTestJob>();
        builder.AddCronBackgroundJob<Sunday2230UtcCronTestJob>();
    }
}
