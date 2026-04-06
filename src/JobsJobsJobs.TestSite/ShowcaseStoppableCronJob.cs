using JobsJobsJobs.BackgroundJobs;
using Umbraco.Cms.Core.Sync;

namespace JobsJobsJobs.TestSite;

internal sealed class ShowcaseStoppableCronJob : CronBackgroundJobBase, IStoppableCronBackgroundJob
{
    private readonly IBackgroundJobExecutionCancellation _executionCancellation;
    private readonly ILogger<ShowcaseStoppableCronJob> _logger;
    private readonly IBackgroundJobRunLogWriter<ShowcaseStoppableCronJob> _runLogWriter;
    private int _runCount;

    public ShowcaseStoppableCronJob(
        IBackgroundJobExecutionCancellation executionCancellation,
        ILogger<ShowcaseStoppableCronJob> logger,
        IBackgroundJobRunLogWriter<ShowcaseStoppableCronJob> runLogWriter
    )
    {
        _executionCancellation = executionCancellation;
        _logger = logger;
        _runLogWriter = runLogWriter;
    }

    public override string CronExpression => "*/15 * * * *";

    public override TimeZoneInfo TimeZone => TimeZoneInfo.Utc;

    public override ServerRole[] ServerRoles => Enum.GetValues<ServerRole>();

    public override Task RunJobAsync()
    {
        return RunAsync();
    }

    private async Task RunAsync()
    {
        var runNumber = Interlocked.Increment(ref _runCount);

        _logger.LogInformation("ShowcaseStoppableCronJob run {RunNumber} started", runNumber);
        _runLogWriter.Information(this, "Stoppable CRON showcase run started.");
        _runLogWriter.Information(
            this,
            $"This job uses the CRON schedule '{CronExpression}' in timezone '{TimeZone.Id}' and supports cooperative stop requests."
        );

        try
        {
            for (var phase = 1; phase <= 5; phase++)
            {
                _executionCancellation.ThrowIfCancellationRequested(this);
                _runLogWriter.Information(this, $"Processing stoppable CRON phase {phase} of 5.");
                await Task.Delay(TimeSpan.FromSeconds(3), _executionCancellation.GetCancellationToken(this));
            }

            _runLogWriter.Information(this, "Stoppable CRON showcase run completed successfully.");
            _logger.LogInformation("ShowcaseStoppableCronJob run {RunNumber} completed", runNumber);
        }
        catch (OperationCanceledException) when (_executionCancellation.IsStopRequested(this))
        {
            _runLogWriter.Warning(this, "Stop request observed. Stoppable CRON showcase is shutting down cleanly.");
            _logger.LogWarning("ShowcaseStoppableCronJob run {RunNumber} stopped", runNumber);
            throw;
        }
    }
}
