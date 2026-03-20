using JobsJobsJobs.BackgroundJobs;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace JobsJobsJobs.TestSite;

internal sealed class DashboardSmokeTestComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddRecurringBackgroundJob<DashboardSmokeTestJob>();
        builder.AddCronBackgroundJob<DashboardFileWriteStopTestJob>();
        builder.AddRecurringBackgroundJob<DashboardHeavyResourceStressJob>();
        builder.AddRecurringBackgroundJob<DashboardLogStormTestJob>();
        builder.AddCronBackgroundJob<EveryTwoMinutesLongRunningOverlapTestJob>();
        builder.AddCronBackgroundJob<Daily1000To1100UtcCronTestJob>();
        builder.AddCronBackgroundJob<Daily1030To1130UtcCronTestJob>();
    }
}
