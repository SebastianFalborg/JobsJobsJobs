using System;
using System.Threading;
using System.Threading.Tasks;
using JobsJobsJobs.BackgroundJobs;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace JobsJobsJobs.TestSite;

internal sealed class DashboardLogStormTestJob : RecurringBackgroundJobBase, IStoppableRecurringBackgroundJob
{
    private const int TotalLogEntries = 10000;
    private const int BatchSize = 250;
    private readonly IBackgroundJobExecutionCancellation _executionCancellation;
    private readonly ILogger<DashboardLogStormTestJob> _logger;
    private readonly IBackgroundJobRunLogWriter<DashboardLogStormTestJob> _runLogWriter;
    private int _runCount;

    public DashboardLogStormTestJob(
        IBackgroundJobExecutionCancellation executionCancellation,
        ILogger<DashboardLogStormTestJob> logger,
        IBackgroundJobRunLogWriter<DashboardLogStormTestJob> runLogWriter
    )
    {
        _executionCancellation = executionCancellation;
        _logger = logger;
        _runLogWriter = runLogWriter;
    }

    public override TimeSpan Period => TimeSpan.FromDays(1);

    public override TimeSpan Delay => TimeSpan.FromDays(1);

    public override async Task RunJobAsync()
    {
        var runNumber = Interlocked.Increment(ref _runCount);

        _logger.LogInformation(
            "DashboardLogStormTestJob run {RunNumber} started and will write {TotalLogEntries} log entries",
            runNumber,
            TotalLogEntries
        );
        _runLogWriter.Warning($"Log storm run {runNumber} started. Writing {TotalLogEntries} log entries in batches of {BatchSize}.");

        try
        {
            for (var start = 1; start <= TotalLogEntries; start += BatchSize)
            {
                _executionCancellation.ThrowIfCancellationRequested();
                var end = Math.Min(start + BatchSize - 1, TotalLogEntries);

                for (var entryNumber = start; entryNumber <= end; entryNumber++)
                {
                    _runLogWriter.Information(
                        $"Log storm run {runNumber} entry {entryNumber} of {TotalLogEntries}. Batch {((entryNumber - 1) / BatchSize) + 1}."
                    );
                }

                _runLogWriter.Warning($"Log storm run {runNumber} flushed entries {start}-{end}.");
                await Task.Yield();
            }

            _runLogWriter.Warning($"Log storm run {runNumber} completed successfully after writing {TotalLogEntries} entries.");
            _logger.LogInformation("DashboardLogStormTestJob run {RunNumber} completed", runNumber);
        }
        catch (OperationCanceledException) when (_executionCancellation.IsStopRequested)
        {
            _runLogWriter.Warning($"Log storm run {runNumber} observed a stop request and is shutting down.");
            _logger.LogWarning("DashboardLogStormTestJob run {RunNumber} stopped", runNumber);
            throw;
        }
    }
}
