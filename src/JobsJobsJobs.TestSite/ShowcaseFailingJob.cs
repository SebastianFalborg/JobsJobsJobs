using JobsJobsJobs.BackgroundJobs;

namespace JobsJobsJobs.TestSite;

internal sealed class ShowcaseFailingJob : RecurringBackgroundJobBase
{
    private readonly ILogger<ShowcaseFailingJob> _logger;
    private readonly IBackgroundJobRunLogWriter<ShowcaseFailingJob> _runLogWriter;
    private int _runCount;

    public ShowcaseFailingJob(ILogger<ShowcaseFailingJob> logger, IBackgroundJobRunLogWriter<ShowcaseFailingJob> runLogWriter)
    {
        _logger = logger;
        _runLogWriter = runLogWriter;
    }

    public override TimeSpan Period => TimeSpan.FromMinutes(20);

    public override TimeSpan Delay => TimeSpan.FromMinutes(1);

    public override async Task RunJobAsync()
    {
        var runNumber = Interlocked.Increment(ref _runCount);

        _logger.LogInformation("ShowcaseFailingJob run {RunNumber} started", runNumber);
        _runLogWriter.Warning("This showcase job is designed to fail intentionally so the dashboard can demonstrate failed runs.");
        _runLogWriter.Information(
            $"The job will fail at the end of this run. Current schedule is every {Period:c} with an initial delay of {Delay:c}."
        );

        for (var step = 1; step <= 2; step++)
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            _runLogWriter.Information($"Completed failing showcase step {step} of 2.");
        }

        const string failureMessage = "Intentional showcase failure. This job exists to demonstrate the failed-run experience in the dashboard.";
        _runLogWriter.Error(failureMessage);
        _logger.LogWarning("ShowcaseFailingJob run {RunNumber} is failing intentionally", runNumber);

        throw new InvalidOperationException(failureMessage);
    }
}
