using JobsJobsJobs.Core.BackgroundJobs;

namespace JobsJobsJobs.TestSite.StressJobs;

internal sealed class StressSpammyLogJob : RecurringBackgroundJobBase
{
    private const int LogLinesPerRun = 200;

    private readonly ILogger<StressSpammyLogJob> _logger;
    private readonly IBackgroundJobRunLogWriter<StressSpammyLogJob> _runLogWriter;
    private int _runCount;

    public StressSpammyLogJob(ILogger<StressSpammyLogJob> logger, IBackgroundJobRunLogWriter<StressSpammyLogJob> runLogWriter)
    {
        _logger = logger;
        _runLogWriter = runLogWriter;
    }

    public override TimeSpan Period => TimeSpan.FromSeconds(5);

    public override TimeSpan Delay => TimeSpan.FromSeconds(15);

    public override async Task RunJobAsync()
    {
        var runNumber = Interlocked.Increment(ref _runCount);

        _logger.LogInformation("StressSpammyLogJob run {RunNumber} started", runNumber);
        _runLogWriter.Information(this, $"Spammy log stress run {runNumber} started. Writing {LogLinesPerRun} log entries.");

        for (var i = 1; i <= LogLinesPerRun; i++)
        {
            _runLogWriter.Information(this, $"Spam line {i:D4} of run {runNumber} at {DateTime.UtcNow:O}.");
        }

        _runLogWriter.Information(this, $"Spammy log stress run {runNumber} completed.");
        _logger.LogInformation("StressSpammyLogJob run {RunNumber} completed", runNumber);
        await Task.CompletedTask;
    }
}
