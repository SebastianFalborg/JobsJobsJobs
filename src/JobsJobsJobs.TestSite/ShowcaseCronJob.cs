using JobsJobsJobs.BackgroundJobs;
using Umbraco.Cms.Core.Sync;

namespace JobsJobsJobs.TestSite;

internal sealed class ShowcaseCronJob : CronBackgroundJobBase
{
    private readonly ILogger<ShowcaseCronJob> _logger;
    private readonly IBackgroundJobRunLogWriter<ShowcaseCronJob> _runLogWriter;
    private int _runCount;

    public ShowcaseCronJob(ILogger<ShowcaseCronJob> logger, IBackgroundJobRunLogWriter<ShowcaseCronJob> runLogWriter)
    {
        _logger = logger;
        _runLogWriter = runLogWriter;
    }

    public override string CronExpression => "*/10 * * * *";

    public override TimeZoneInfo TimeZone => TimeZoneInfo.Utc;

    public override ServerRole[] ServerRoles => Enum.GetValues<ServerRole>();

    public override async Task RunJobAsync()
    {
        var runNumber = Interlocked.Increment(ref _runCount);

        _logger.LogInformation("ShowcaseCronJob run {RunNumber} started", runNumber);
        _runLogWriter.Information("CRON showcase run started.");
        _runLogWriter.Information($"This job uses the CRON schedule '{CronExpression}' in timezone '{TimeZone.Id}'.");

        for (var step = 1; step <= 2; step++)
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            _runLogWriter.Information($"Completed CRON showcase step {step} of 2.");
        }

        _runLogWriter.Information("CRON showcase run completed successfully.");
        _logger.LogInformation("ShowcaseCronJob run {RunNumber} completed", runNumber);
    }
}
