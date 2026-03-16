using System;
using System.Threading;
using System.Threading.Tasks;
using JobsJobsJobs.BackgroundJobs;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace JobsJobsJobs.TestSite;

internal sealed class Sunday2200UtcCronTestJob : CronBackgroundJobBase, IStoppableCronBackgroundJob
{
    private readonly IBackgroundJobExecutionCancellation _executionCancellation;
    private readonly ILogger<Sunday2200UtcCronTestJob> _logger;
    private readonly IBackgroundJobRunLogWriter<Sunday2200UtcCronTestJob> _runLogWriter;
    private int _runCount;

    public Sunday2200UtcCronTestJob(
        IBackgroundJobExecutionCancellation executionCancellation,
        ILogger<Sunday2200UtcCronTestJob> logger,
        IBackgroundJobRunLogWriter<Sunday2200UtcCronTestJob> runLogWriter)
    {
        _executionCancellation = executionCancellation;
        _logger = logger;
        _runLogWriter = runLogWriter;
    }

    public override string CronExpression => "* 22-23 * * SUN";

    public override TimeZoneInfo TimeZone => TimeZoneInfo.Utc;

    public override ServerRole[] ServerRoles => Enum.GetValues<ServerRole>();

    public override Task RunJobAsync()
        => RunAsync();

    private async Task RunAsync()
    {
        var runNumber = Interlocked.Increment(ref _runCount);

        _logger.LogInformation(
            "Sunday2200UtcCronTestJob run {RunNumber} started. UtcNow: {UtcNow}",
            runNumber,
            DateTimeOffset.UtcNow);

        _runLogWriter.Information($"CRON run {runNumber} started.");

        for (var second = 1; second <= 10; second++)
        {
            _executionCancellation.ThrowIfCancellationRequested();
            await Task.Delay(TimeSpan.FromSeconds(1), _executionCancellation.CancellationToken);
            _runLogWriter.Information($"Completed simulated CRON second {second} of 10.");
        }

        _runLogWriter.Information("CRON run completed successfully.");
        _logger.LogInformation("Sunday2200UtcCronTestJob run {RunNumber} completed", runNumber);
    }
}
