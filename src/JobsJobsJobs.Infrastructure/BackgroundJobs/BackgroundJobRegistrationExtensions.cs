using System.Reflection;
using JobsJobsJobs.Core.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Umbraco.Extensions;

namespace JobsJobsJobs.Infrastructure.BackgroundJobs;

public static class BackgroundJobRegistrationExtensions
{
    public static IUmbracoBuilder AddRecurringBackgroundJob<TJob>(this IUmbracoBuilder builder)
        where TJob : class, IRecurringBackgroundJob
    {
        builder.Services.AddRecurringBackgroundJob<TJob>();
        return builder;
    }
}

public static class BackgroundJobCronRegistrationExtensions
{
    private static readonly MethodInfo s_addStoppableCronBackgroundJobCoreMethod = typeof(BackgroundJobCronRegistrationExtensions).GetMethod(
        nameof(AddStoppableCronBackgroundJobCore),
        BindingFlags.NonPublic | BindingFlags.Static
    )!;

    public static IUmbracoBuilder AddCronBackgroundJob<TJob>(this IUmbracoBuilder builder)
        where TJob : class, ICronBackgroundJob
    {
        builder.Services.AddCronBackgroundJob<TJob>();
        return builder;
    }

    public static IUmbracoBuilder AddStoppableCronBackgroundJob<TJob>(this IUmbracoBuilder builder)
        where TJob : class, IStoppableCronBackgroundJob
    {
        builder.Services.AddCronBackgroundJob<TJob>();
        return builder;
    }

    public static IServiceCollection AddCronBackgroundJob<TJob>(this IServiceCollection services)
        where TJob : class, ICronBackgroundJob
    {
        services.AddTransient<TJob>();

        if (typeof(IStoppableCronBackgroundJob).IsAssignableFrom(typeof(TJob)))
        {
            s_addStoppableCronBackgroundJobCoreMethod.MakeGenericMethod(typeof(TJob)).Invoke(null, new object[] { services });
            return services;
        }

        services.AddRecurringBackgroundJob<CronRecurringBackgroundJobAdapter<TJob>>();
        return services;
    }

    public static IServiceCollection AddStoppableCronBackgroundJob<TJob>(this IServiceCollection services)
        where TJob : class, IStoppableCronBackgroundJob
    {
        return services.AddCronBackgroundJob<TJob>();
    }

    private static void AddStoppableCronBackgroundJobCore<TJob>(IServiceCollection services)
        where TJob : class, IStoppableCronBackgroundJob => services.AddRecurringBackgroundJob<StoppableCronRecurringBackgroundJobAdapter<TJob>>();
}
