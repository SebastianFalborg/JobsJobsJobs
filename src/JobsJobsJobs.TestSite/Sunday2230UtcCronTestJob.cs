using System;
using System.Threading;
using System.Threading.Tasks;
using JobsJobsJobs.BackgroundJobs;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Sync;

namespace JobsJobsJobs.TestSite;

internal sealed class Sunday2230UtcCronTestJob : CronBackgroundJobBase
{
    private readonly ILogger<Sunday2230UtcCronTestJob> _logger;
    private int _runCount;

    public Sunday2230UtcCronTestJob(ILogger<Sunday2230UtcCronTestJob> logger) => _logger = logger;

    public override string CronExpression => "30-59 22 * * SUN; * 23 * * SUN";

    public override TimeZoneInfo TimeZone => TimeZoneInfo.Utc;

    public override ServerRole[] ServerRoles => Enum.GetValues<ServerRole>();

    public override Task RunJobAsync()
    {
        var runNumber = Interlocked.Increment(ref _runCount);
        _logger.LogInformation(
            "Sunday2230UtcCronTestJob ran. Run {RunNumber}. UtcNow: {UtcNow}",
            runNumber,
            DateTimeOffset.UtcNow);

        return Task.CompletedTask;
    }
}
