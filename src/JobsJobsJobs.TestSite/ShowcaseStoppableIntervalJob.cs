using JobsJobsJobs.BackgroundJobs;

namespace JobsJobsJobs.TestSite;

internal sealed class ShowcaseStoppableIntervalJob : RecurringBackgroundJobBase, IStoppableRecurringBackgroundJob
{
    private readonly IBackgroundJobExecutionCancellation _executionCancellation;
    private readonly ILogger<ShowcaseStoppableIntervalJob> _logger;
    private readonly IBackgroundJobRunLogWriter<ShowcaseStoppableIntervalJob> _runLogWriter;
    private int _runCount;

    public ShowcaseStoppableIntervalJob(
        IBackgroundJobExecutionCancellation executionCancellation,
        ILogger<ShowcaseStoppableIntervalJob> logger,
        IBackgroundJobRunLogWriter<ShowcaseStoppableIntervalJob> runLogWriter
    )
    {
        _executionCancellation = executionCancellation;
        _logger = logger;
        _runLogWriter = runLogWriter;
    }

    public override TimeSpan Period => TimeSpan.FromMinutes(15);

    public override TimeSpan Delay => TimeSpan.FromMinutes(5);

    public override Task RunJobAsync()
    {
        return RunAsync();
    }

    private async Task RunAsync()
    {
        var runNumber = Interlocked.Increment(ref _runCount);

        _logger.LogInformation("ShowcaseStoppableIntervalJob run {RunNumber} started", runNumber);
        _runLogWriter.Information(this, "Stoppable interval showcase run started.");
        _runLogWriter.Information(this, $"This job uses a fixed interval schedule of {Period:c} and supports cooperative stop requests.");

        try
        {
            for (var batch = 1; batch <= 6; batch++)
            {
                _executionCancellation.ThrowIfCancellationRequested(this);
                _runLogWriter.Information(this, $"Processing stoppable interval batch {batch} of 6.");
                await Task.Delay(TimeSpan.FromSeconds(3), _executionCancellation.GetCancellationToken(this));
            }

            _runLogWriter.Information(this, "Stoppable interval showcase run completed successfully.");
            _logger.LogInformation("ShowcaseStoppableIntervalJob run {RunNumber} completed", runNumber);
        }
        catch (OperationCanceledException) when (_executionCancellation.IsStopRequested(this))
        {
            _runLogWriter.Warning(this, "Stop request observed. Stoppable interval showcase is shutting down cleanly.");
            _logger.LogWarning("ShowcaseStoppableIntervalJob run {RunNumber} stopped", runNumber);
            throw;
        }
    }
}
