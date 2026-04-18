using System.Security.Cryptography;
using JobsJobsJobs.Core.BackgroundJobs;

namespace JobsJobsJobs.TestSite.StressJobs;

internal sealed class StressHeavyResourceJob : RecurringBackgroundJobBase
{
    private static readonly TimeSpan s_workDuration = TimeSpan.FromSeconds(30);

    private readonly ILogger<StressHeavyResourceJob> _logger;
    private readonly IBackgroundJobRunLogWriter<StressHeavyResourceJob> _runLogWriter;
    private int _runCount;

    public StressHeavyResourceJob(ILogger<StressHeavyResourceJob> logger, IBackgroundJobRunLogWriter<StressHeavyResourceJob> runLogWriter)
    {
        _logger = logger;
        _runLogWriter = runLogWriter;
    }

    public override TimeSpan Period => TimeSpan.FromMinutes(1);

    public override TimeSpan Delay => TimeSpan.FromSeconds(45);

    public override async Task RunJobAsync()
    {
        var runNumber = Interlocked.Increment(ref _runCount);

        _logger.LogInformation("StressHeavyResourceJob run {RunNumber} started", runNumber);
        _runLogWriter.Information(this, $"Heavy-resource stress run {runNumber} started. Will burn CPU and allocate for {s_workDuration:c}.");

        var endAt = DateTime.UtcNow.Add(s_workDuration);
        var buffer = new byte[1024 * 1024];
        var hashes = 0L;
        using var sha = SHA256.Create();
        while (DateTime.UtcNow < endAt)
        {
            RandomNumberGenerator.Fill(buffer);
            _ = sha.ComputeHash(buffer);
            hashes++;

            if (hashes % 50 == 0)
            {
                _runLogWriter.Information(this, $"Heavy-resource stress run {runNumber}: {hashes} SHA-256 hashes over 1MB buffers so far.");
                await Task.Yield();
            }
        }

        _runLogWriter.Information(this, $"Heavy-resource stress run {runNumber} completed. Total hashes: {hashes}.");
        _logger.LogInformation("StressHeavyResourceJob run {RunNumber} completed with {Hashes} hashes", runNumber, hashes);
    }
}
