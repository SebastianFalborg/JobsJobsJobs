using JobsJobsJobs.Core.BackgroundJobs;

namespace JobsJobsJobs.TestSite;

internal sealed class ShowcaseIntervalJob : RecurringBackgroundJobBase
{
    private readonly ILogger<ShowcaseIntervalJob> _logger;
    private readonly IBackgroundJobRunLogWriter<ShowcaseIntervalJob> _runLogWriter;
    private int _runCount;

    public ShowcaseIntervalJob(ILogger<ShowcaseIntervalJob> logger, IBackgroundJobRunLogWriter<ShowcaseIntervalJob> runLogWriter)
    {
        _logger = logger;
        _runLogWriter = runLogWriter;
    }

    public override TimeSpan Period => TimeSpan.FromMinutes(10);

    public override TimeSpan Delay => TimeSpan.FromMinutes(5);

    public override async Task RunJobAsync()
    {
        var runNumber = Interlocked.Increment(ref _runCount);

        _logger.LogInformation("ShowcaseIntervalJob run {RunNumber} started", runNumber);
        _runLogWriter.Information(this, "Interval showcase run started.");
        _runLogWriter.Information(this, $"This job uses a fixed interval schedule of {Period:c}.");

        for (var step = 1; step <= 3; step++)
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            _runLogWriter.Information(this, $"Completed interval showcase step {step} of 3.");
        }

        _runLogWriter.Information(this, "Interval showcase run completed successfully.");
        _logger.LogInformation("ShowcaseIntervalJob run {RunNumber} completed", runNumber);
    }
}
