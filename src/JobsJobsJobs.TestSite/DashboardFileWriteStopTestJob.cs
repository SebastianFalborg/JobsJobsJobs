using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JobsJobsJobs.BackgroundJobs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace JobsJobsJobs.TestSite;

internal sealed class DashboardFileWriteStopTestJob : RecurringBackgroundJobBase, IStoppableRecurringBackgroundJob
{
    private readonly IBackgroundJobExecutionCancellation _executionCancellation;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<DashboardFileWriteStopTestJob> _logger;
    private readonly IBackgroundJobRunLogWriter<DashboardFileWriteStopTestJob> _runLogWriter;
    private int _runCount;

    public DashboardFileWriteStopTestJob(
        IBackgroundJobExecutionCancellation executionCancellation,
        IHostEnvironment hostEnvironment,
        ILogger<DashboardFileWriteStopTestJob> logger,
        IBackgroundJobRunLogWriter<DashboardFileWriteStopTestJob> runLogWriter)
    {
        _executionCancellation = executionCancellation;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
        _runLogWriter = runLogWriter;
    }

    public override TimeSpan Period => TimeSpan.FromDays(1);

    public override TimeSpan Delay => TimeSpan.FromDays(1);

    public override async Task RunJobAsync()
    {
        var runNumber = Interlocked.Increment(ref _runCount);
        var outputDirectory = Path.Combine(_hostEnvironment.ContentRootPath, "App_Data", "JobsJobsJobs");
        var outputPath = Path.Combine(outputDirectory, "stop-demo-log.txt");

        Directory.CreateDirectory(outputDirectory);

        await AppendLineAsync(outputPath, $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] Run {runNumber} started.");
        _runLogWriter.Information($"Writing loop output to {outputPath}.");
        _logger.LogInformation("DashboardFileWriteStopTestJob run {RunNumber} writing to {OutputPath}", runNumber, outputPath);

        try
        {
            for (var iteration = 1; iteration <= 30; iteration++)
            {
                _executionCancellation.ThrowIfCancellationRequested();

                var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] Run {runNumber} iteration {iteration}. Stop requested: {_executionCancellation.IsStopRequested}.";
                await AppendLineAsync(outputPath, line);
                _runLogWriter.Information($"Wrote iteration {iteration}.");
                await Task.Delay(TimeSpan.FromSeconds(1), _executionCancellation.CancellationToken);
            }

            await AppendLineAsync(outputPath, $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] Run {runNumber} completed successfully.");
            _runLogWriter.Information("File writing test run completed successfully.");
        }
        catch (OperationCanceledException) when (_executionCancellation.IsStopRequested)
        {
            await AppendLineAsync(outputPath, $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] Run {runNumber} observed stop request and is shutting down cleanly.");
            _runLogWriter.Warning("Stop request observed while writing file output.");
            throw;
        }
    }

    private static Task AppendLineAsync(string outputPath, string line)
        => File.AppendAllTextAsync(outputPath, line + Environment.NewLine);
}
