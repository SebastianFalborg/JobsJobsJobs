using JobsJobsJobs.Core.BackgroundJobs;

namespace Umbraco.Cms.JobsJobsJobs.Tests.FakeJobs;

internal sealed class UmbracoNamespacedJob : RecurringBackgroundJobBase
{
    public override TimeSpan Period => TimeSpan.FromMinutes(1);

    public override Task RunJobAsync() => Task.CompletedTask;
}
