using JobsJobsJobs.BackgroundJobs;
using Umbraco.Cms.Core.Composing;

namespace JobsJobsJobs.TestSite;

internal sealed class BackgroundJobComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddRecurringBackgroundJob<ShowcaseIntervalJob>();
        builder.AddRecurringBackgroundJob<ShowcaseStoppableIntervalJob>();
        builder.AddRecurringBackgroundJob<ShowcaseFailingJob>();
        builder.AddCronBackgroundJob<ShowcaseCronJob>();
        builder.AddCronBackgroundJob<ShowcaseStoppableCronJob>();
    }
}
