using JobsJobsJobs.Core.BackgroundJobs;

namespace JobsJobsJobs.TestSite.StressJobs;

internal sealed class StressLongStoppableJob : RecurringBackgroundJobBase, IStoppableRecurringBackgroundJob
{
    private static readonly TimeSpan s_workDuration = TimeSpan.FromMinutes(8);
    private static readonly TimeSpan s_stepInterval = TimeSpan.FromSeconds(10);

    private readonly IBackgroundJobExecutionCancellation _executionCancellation;
    private readonly ILogger<StressLongStoppableJob> _logger;
    private readonly IBackgroundJobRunLogWriter<StressLongStoppableJob> _runLogWriter;
    private int _runCount;

    public StressLongStoppableJob(
        IBackgroundJobExecutionCancellation executionCancellation,
        ILogger<StressLongStoppableJob> logger,
        IBackgroundJobRunLogWriter<StressLongStoppableJob> runLogWriter
    )
    {
        _executionCancellation = executionCancellation;
        _logger = logger;
        _runLogWriter = runLogWriter;
    }

    public override TimeSpan Period => TimeSpan.FromMinutes(10);

    public override TimeSpan Delay => TimeSpan.FromSeconds(60);

    public override Task RunJobAsync()
    {
        return RunAsync();
    }

    private async Task RunAsync()
    {
        var runNumber = Interlocked.Increment(ref _runCount);

        _logger.LogInformation("StressLongStoppableJob run {RunNumber} started", runNumber);
        _runLogWriter.Information(
            this,
            $"Long stoppable stress run {runNumber} started. Duration {s_workDuration:c} with cooperative stop support. Click Stop to exercise cancellation."
        );

        try
        {
            var endAt = DateTime.UtcNow.Add(s_workDuration);
            var step = 0;
            while (DateTime.UtcNow < endAt)
            {
                _executionCancellation.ThrowIfCancellationRequested(this);
                step++;
                _runLogWriter.Information(this, $"Long stoppable run {runNumber} step {step} at {DateTime.UtcNow:O}.");
                await Task.Delay(s_stepInterval, _executionCancellation.GetCancellationToken(this));
            }

            _runLogWriter.Information(this, $"Long stoppable stress run {runNumber} completed cleanly.");
            _logger.LogInformation("StressLongStoppableJob run {RunNumber} completed", runNumber);
        }
        catch (OperationCanceledException) when (_executionCancellation.IsStopRequested(this))
        {
            _runLogWriter.Warning(this, $"Stop request observed for long stoppable run {runNumber}. Shutting down cleanly.");
            _logger.LogWarning("StressLongStoppableJob run {RunNumber} stopped", runNumber);
            throw;
        }
    }
}
