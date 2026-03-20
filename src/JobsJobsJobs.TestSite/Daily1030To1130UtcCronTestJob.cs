using System;
using System.Threading;
using System.Threading.Tasks;
using JobsJobsJobs.BackgroundJobs;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Sync;

namespace JobsJobsJobs.TestSite;

internal sealed class Daily1030To1130UtcCronTestJob : CronBackgroundJobBase
{
    private readonly ILogger<Daily1030To1130UtcCronTestJob> _logger;
    private int _runCount;

    public Daily1030To1130UtcCronTestJob(ILogger<Daily1030To1130UtcCronTestJob> logger) => _logger = logger;

    public override string CronExpression => "30-59 10 * * *; 0-30 11 * * *";

    public override TimeZoneInfo TimeZone => TimeZoneInfo.Utc;

    public override ServerRole[] ServerRoles => Enum.GetValues<ServerRole>();

    public override Task RunJobAsync()
    {
        var runNumber = Interlocked.Increment(ref _runCount);
        _logger.LogInformation("Daily1030To1130UtcCronTestJob ran. Run {RunNumber}. UtcNow: {UtcNow}", runNumber, DateTimeOffset.UtcNow);

        return Task.CompletedTask;
    }
}
