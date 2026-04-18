using JobsJobsJobs.Core.BackgroundJobs;

namespace JobsJobsJobs.TestSite.StressJobs;

internal sealed class StressOverlapIntervalJob : RecurringBackgroundJobBase
{
    private const int LongRunEveryN = 5;
    private static readonly TimeSpan s_longRunDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan s_normalRunDuration = TimeSpan.FromSeconds(5);

    private readonly ILogger<StressOverlapIntervalJob> _logger;
    private readonly IBackgroundJobRunLogWriter<StressOverlapIntervalJob> _runLogWriter;
    private int _runCount;

    public StressOverlapIntervalJob(ILogger<StressOverlapIntervalJob> logger, IBackgroundJobRunLogWriter<StressOverlapIntervalJob> runLogWriter)
    {
        _logger = logger;
        _runLogWriter = runLogWriter;
    }

    public override TimeSpan Period => TimeSpan.FromMinutes(2);

    public override TimeSpan Delay => TimeSpan.FromSeconds(30);

    public override async Task RunJobAsync()
    {
        var runNumber = Interlocked.Increment(ref _runCount);
        var isLongRun = runNumber % LongRunEveryN == 0;
        var duration = isLongRun ? s_longRunDuration : s_normalRunDuration;

        _logger.LogInformation(
            "StressOverlapIntervalJob run {RunNumber} started (isLongRun={IsLongRun}, duration={Duration})",
            runNumber,
            isLongRun,
            duration
        );
        _runLogWriter.Information(
            this,
            $"Overlap stress run {runNumber} started. Duration {duration:c}. Period is {Period:c} so every {LongRunEveryN}th run forces a scheduling overlap."
        );

        var endAt = DateTime.UtcNow.Add(duration);
        var beat = 0;
        while (DateTime.UtcNow < endAt)
        {
            await Task.Delay(TimeSpan.FromSeconds(15));
            beat++;
            _runLogWriter.Information(this, $"Overlap stress run {runNumber} heartbeat {beat} ({DateTime.UtcNow:O}).");
        }

        _runLogWriter.Information(this, $"Overlap stress run {runNumber} completed.");
        _logger.LogInformation("StressOverlapIntervalJob run {RunNumber} completed", runNumber);
    }
}
