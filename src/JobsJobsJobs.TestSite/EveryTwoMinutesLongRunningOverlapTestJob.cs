using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using JobsJobsJobs.BackgroundJobs;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace JobsJobsJobs.TestSite;

internal sealed class EveryTwoMinutesLongRunningOverlapTestJob : CronBackgroundJobBase, IStoppableCronBackgroundJob
{
    private static readonly TimeSpan ShortRunDuration = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan LongRunDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);
    private readonly IBackgroundJobExecutionCancellation _executionCancellation;
    private readonly ILogger<EveryTwoMinutesLongRunningOverlapTestJob> _logger;
    private readonly IBackgroundJobRunLogWriter<EveryTwoMinutesLongRunningOverlapTestJob> _runLogWriter;
    private int _runCount;

    public EveryTwoMinutesLongRunningOverlapTestJob(
        IBackgroundJobExecutionCancellation executionCancellation,
        ILogger<EveryTwoMinutesLongRunningOverlapTestJob> logger,
        IBackgroundJobRunLogWriter<EveryTwoMinutesLongRunningOverlapTestJob> runLogWriter
    )
    {
        _executionCancellation = executionCancellation;
        _logger = logger;
        _runLogWriter = runLogWriter;
    }

    public override string CronExpression => "*/2 * * * *";

    public override TimeZoneInfo TimeZone => TimeZoneInfo.Utc;

    public override ServerRole[] ServerRoles => Enum.GetValues<ServerRole>();

    public override Task RunJobAsync() => RunAsync();

    private async Task RunAsync()
    {
        var runNumber = Interlocked.Increment(ref _runCount);
        var targetDuration = runNumber % 5 == 0 ? LongRunDuration : ShortRunDuration;
        var runKind = targetDuration == LongRunDuration ? "long" : "short";
        var stopwatch = Stopwatch.StartNew();
        var nextHeartbeat = HeartbeatInterval;

        _logger.LogInformation(
            "EveryTwoMinutesLongRunningOverlapTestJob run {RunNumber} started as a {RunKind} run with target duration {TargetDuration}",
            runNumber,
            runKind,
            targetDuration
        );
        _runLogWriter.Warning(
            $"Overlap test run {runNumber} started as a {runKind} run. Target duration: {targetDuration:hh\\:mm\\:ss}. Cron: {CronExpression}."
        );

        try
        {
            while (stopwatch.Elapsed < targetDuration)
            {
                _executionCancellation.ThrowIfCancellationRequested();

                if (stopwatch.Elapsed >= nextHeartbeat)
                {
                    _runLogWriter.Information(
                        $"Overlap test run {runNumber} heartbeat at {stopwatch.Elapsed:hh\\:mm\\:ss}. Stop requested: {_executionCancellation.IsStopRequested}."
                    );
                    nextHeartbeat += HeartbeatInterval;
                }

                await Task.Delay(TimeSpan.FromSeconds(1), _executionCancellation.CancellationToken);
            }

            _runLogWriter.Warning($"Overlap test run {runNumber} finished after {stopwatch.Elapsed:hh\\:mm\\:ss}. This was a {runKind} run.");
            _logger.LogInformation(
                "EveryTwoMinutesLongRunningOverlapTestJob run {RunNumber} completed after {Elapsed}",
                runNumber,
                stopwatch.Elapsed
            );
        }
        catch (OperationCanceledException) when (_executionCancellation.IsStopRequested)
        {
            _runLogWriter.Warning($"Overlap test run {runNumber} observed stop after {stopwatch.Elapsed:hh\\:mm\\:ss}. This was a {runKind} run.");
            _logger.LogWarning("EveryTwoMinutesLongRunningOverlapTestJob run {RunNumber} stopped after {Elapsed}", runNumber, stopwatch.Elapsed);
            throw;
        }
    }
}
