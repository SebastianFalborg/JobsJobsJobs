using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using JobsJobsJobs.BackgroundJobs;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace JobsJobsJobs.TestSite;

internal sealed class DashboardHeavyResourceStressJob : RecurringBackgroundJobBase, IStoppableRecurringBackgroundJob
{
    private const int AllocationBlockSizeBytes = 8 * 1024 * 1024;
    private const int AllocationBlockCount = 12;
    private const int BurstCount = 24;
    private static readonly TimeSpan BurstDuration = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan CooldownBetweenBursts = TimeSpan.FromMilliseconds(400);
    private readonly IBackgroundJobExecutionCancellation _executionCancellation;
    private readonly ILogger<DashboardHeavyResourceStressJob> _logger;
    private readonly IBackgroundJobRunLogWriter<DashboardHeavyResourceStressJob> _runLogWriter;
    private int _runCount;

    public DashboardHeavyResourceStressJob(
        IBackgroundJobExecutionCancellation executionCancellation,
        ILogger<DashboardHeavyResourceStressJob> logger,
        IBackgroundJobRunLogWriter<DashboardHeavyResourceStressJob> runLogWriter
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
        var allocatedMegabytes = AllocationBlockCount * AllocationBlockSizeBytes / 1024 / 1024;
        var allocations = new List<byte[]>(AllocationBlockCount);
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "DashboardHeavyResourceStressJob run {RunNumber} started with approximately {AllocatedMegabytes} MB of managed allocations",
            runNumber,
            allocatedMegabytes
        );
        _runLogWriter.Warning($"Heavy resource stress run {runNumber} started. Target managed allocation is approximately {allocatedMegabytes} MB.");

        try
        {
            for (var blockIndex = 0; blockIndex < AllocationBlockCount; blockIndex++)
            {
                _executionCancellation.ThrowIfCancellationRequested();

                var block = GC.AllocateUninitializedArray<byte>(AllocationBlockSizeBytes);
                FillBlock(block, runNumber, blockIndex);
                allocations.Add(block);
                _runLogWriter.Information(
                    $"Allocated block {blockIndex + 1} of {AllocationBlockCount} ({AllocationBlockSizeBytes / 1024 / 1024} MB)."
                );
            }

            for (var burstIndex = 1; burstIndex <= BurstCount; burstIndex++)
            {
                _executionCancellation.ThrowIfCancellationRequested();
                ConsumeCpu(allocations, runNumber, burstIndex);
                _runLogWriter.Information(
                    $"Completed CPU burst {burstIndex} of {BurstCount}. Elapsed: {stopwatch.Elapsed:hh\\:mm\\:ss}. Stop requested: {_executionCancellation.IsStopRequested}."
                );
                await Task.Delay(CooldownBetweenBursts, _executionCancellation.CancellationToken);
            }

            _runLogWriter.Warning(
                $"Heavy resource stress run {runNumber} completed after {stopwatch.Elapsed:hh\\:mm\\:ss}. Managed allocations will now be released."
            );
            _logger.LogInformation("DashboardHeavyResourceStressJob run {RunNumber} completed in {Elapsed}", runNumber, stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (_executionCancellation.IsStopRequested)
        {
            _runLogWriter.Warning($"Heavy resource stress run {runNumber} observed a stop request after {stopwatch.Elapsed:hh\\:mm\\:ss}.");
            _logger.LogWarning("DashboardHeavyResourceStressJob run {RunNumber} stopped after {Elapsed}", runNumber, stopwatch.Elapsed);
            throw;
        }
        finally
        {
            allocations.Clear();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
        }
    }

    private static void FillBlock(byte[] block, int runNumber, int blockIndex)
    {
        for (var offset = 0; offset < block.Length; offset += 4096)
        {
            block[offset] = unchecked((byte)(runNumber + blockIndex + offset));
        }
    }

    private static void ConsumeCpu(IReadOnlyList<byte[]> allocations, int runNumber, int burstIndex)
    {
        var burstStopwatch = Stopwatch.StartNew();
        var checksum = 0L;

        while (burstStopwatch.Elapsed < BurstDuration)
        {
            for (var allocationIndex = 0; allocationIndex < allocations.Count; allocationIndex++)
            {
                var block = allocations[allocationIndex];

                for (var offset = 0; offset < block.Length; offset += 4096)
                {
                    block[offset] = unchecked((byte)(block[offset] + burstIndex + allocationIndex));
                    checksum += block[offset];
                }
            }

            checksum ^= runNumber;
        }

        GC.KeepAlive(checksum);
    }
}
