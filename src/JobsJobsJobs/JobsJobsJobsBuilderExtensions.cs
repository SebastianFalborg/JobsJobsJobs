using JobsJobsJobs.Infrastructure.DependencyInjection;
using Umbraco.Cms.Core.DependencyInjection;

namespace JobsJobsJobs;

public static class JobsJobsJobsBuilderExtensions
{
    public static IUmbracoBuilder AddJobsJobsJobs(this IUmbracoBuilder builder) => builder.AddJobsJobsJobsCore().AddJobsJobsJobsInfrastructure();
}
