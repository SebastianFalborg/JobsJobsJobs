using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Umbraco.Extensions;

namespace JobsJobsJobs.BackgroundJobs;

public static class BackgroundJobRegistrationExtensions
{
    public static IUmbracoBuilder AddRecurringBackgroundJob<TJob>(this IUmbracoBuilder builder)
        where TJob : class, IRecurringBackgroundJob
    {
        builder.Services.AddRecurringBackgroundJob<TJob>();
        return builder;
    }
}
