using JobsJobsJobs.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.Configuration;
using Umbraco.Cms.Core.Composing;

namespace JobsJobsJobs.TestSite.StressJobs;

internal sealed class StressJobComposer : IComposer
{
    private const string EnableStressJobsKey = "TestSite:EnableStressJobs";

    public void Compose(IUmbracoBuilder builder)
    {
        if (builder.Config.GetValue(EnableStressJobsKey, defaultValue: false) is false)
        {
            return;
        }

        builder.AddRecurringBackgroundJob<StressOverlapIntervalJob>();
        builder.AddRecurringBackgroundJob<StressHeavyResourceJob>();
        builder.AddRecurringBackgroundJob<StressSpammyLogJob>();
        builder.AddRecurringBackgroundJob<StressLongStoppableJob>();
    }
}
