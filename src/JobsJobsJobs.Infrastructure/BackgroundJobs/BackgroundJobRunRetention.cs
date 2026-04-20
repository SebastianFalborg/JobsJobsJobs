using System.Diagnostics;
using JobsJobsJobs.Core.BackgroundJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NPoco;
using Umbraco.Cms.Infrastructure.Scoping;

namespace JobsJobsJobs.Infrastructure.BackgroundJobs;

internal interface IBackgroundJobRunRetentionService
{
    int SweepOnce();
}

internal sealed class BackgroundJobRunRetentionService : IBackgroundJobRunRetentionService
{
    private static readonly TimeSpan s_interBatchPause = TimeSpan.FromMilliseconds(50);

    private readonly ILogger<BackgroundJobRunRetentionService> _logger;
    private readonly BackgroundJobDashboardOptions _options;
    private readonly IScopeProvider _scopeProvider;

    public BackgroundJobRunRetentionService(
        ILogger<BackgroundJobRunRetentionService> logger,
        IScopeProvider scopeProvider,
        IOptions<BackgroundJobDashboardOptions> options
    )
    {
        _logger = logger;
        _scopeProvider = scopeProvider;
        _options = options.Value;
    }

    public int SweepOnce()
    {
        var retention = _options.RunHistoryRetention;
        if (retention.Enabled is false)
        {
            return 0;
        }

        if (_options.DisablePersistence)
        {
            return 0;
        }

        var batchSize = retention.DeleteBatchSize > 0 ? retention.DeleteBatchSize : 500;
        var now = DateTime.UtcNow;
        var totalRunsDeleted = 0;
        var totalLogsDeleted = 0;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Background job run history retention sweep starting (MaxRunsPerJob={MaxRunsPerJob}, MaxAge={MaxAge}, BatchSize={BatchSize}).",
            retention.MaxRunsPerJob,
            retention.MaxAge,
            batchSize
        );

        try
        {
            var aliases = FetchDistinctAliases();

            _logger.LogDebug("Retention found {AliasCount} distinct aliases in run table: [{Aliases}].", aliases.Count, string.Join(", ", aliases));

            foreach (var alias in aliases)
            {
                var runs = FetchRunsForAlias(alias);
                var idsToDelete = BackgroundJobRunRetentionPlanner.SelectRunsToDelete(runs, retention, now).ToArray();

                _logger.LogDebug(
                    "Retention scanned alias {JobAlias}: {TotalRuns} runs present, {PlannedDeletes} to delete.",
                    alias,
                    runs.Count,
                    idsToDelete.Length
                );

                if (idsToDelete.Length == 0)
                {
                    continue;
                }

                var aliasRunsDeleted = 0;
                var aliasLogsDeleted = 0;
                foreach (var batch in Chunk(idsToDelete, batchSize))
                {
                    var (logs, runs2) = DeleteBatch(batch);
                    aliasLogsDeleted += logs;
                    aliasRunsDeleted += runs2;

                    Thread.Sleep(s_interBatchPause);
                }

                totalLogsDeleted += aliasLogsDeleted;
                totalRunsDeleted += aliasRunsDeleted;

                _logger.LogDebug(
                    "Retention pruned alias {JobAlias}: {RunCount} run rows, {LogCount} log rows.",
                    alias,
                    aliasRunsDeleted,
                    aliasLogsDeleted
                );
            }

            stopwatch.Stop();
            _logger.LogInformation(
                "Background job run history retention sweep completed in {ElapsedMs} ms. Deleted {RunCount} run rows and {LogCount} log rows.",
                stopwatch.ElapsedMilliseconds,
                totalRunsDeleted,
                totalLogsDeleted
            );
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                ex,
                "Background job run history retention sweep failed after {ElapsedMs} ms. Deleted {RunCount} run rows and {LogCount} log rows before failure. Will retry on next interval.",
                stopwatch.ElapsedMilliseconds,
                totalRunsDeleted,
                totalLogsDeleted
            );
        }

        return totalRunsDeleted;
    }

    private IReadOnlyList<string> FetchDistinctAliases()
    {
        using var scope = _scopeProvider.CreateScope();
        var result = scope.Database.Fetch<string>($"SELECT DISTINCT {nameof(BackgroundJobRunDto.JobAlias)} FROM {BackgroundJobRunDto.TableName}");
        scope.Complete();
        return result;
    }

    private IReadOnlyList<BackgroundJobRunRetentionPlanner.RunSummary> FetchRunsForAlias(string alias)
    {
        using var scope = _scopeProvider.CreateScope();
        var rows = scope.Database.Fetch<RunSummaryRow>(
            $"SELECT {nameof(BackgroundJobRunDto.Id)} AS {nameof(RunSummaryRow.Id)}, "
                + $"{nameof(BackgroundJobRunDto.StartedAt)} AS {nameof(RunSummaryRow.StartedAt)} "
                + $"FROM {BackgroundJobRunDto.TableName} "
                + $"WHERE {nameof(BackgroundJobRunDto.JobAlias)} = @0 "
                + $"ORDER BY {nameof(BackgroundJobRunDto.StartedAt)} DESC",
            alias
        );
        scope.Complete();
        return rows.Select(r => new BackgroundJobRunRetentionPlanner.RunSummary(r.Id, r.StartedAt)).ToArray();
    }

    private sealed class RunSummaryRow
    {
        public Guid Id { get; set; }

        public DateTime StartedAt { get; set; }
    }

    private (int LogsDeleted, int RunsDeleted) DeleteBatch(IReadOnlyCollection<Guid> runIds)
    {
        if (runIds.Count == 0)
        {
            return (0, 0);
        }

        var parameters = runIds.Cast<object>().ToArray();
        var placeholders = string.Join(",", Enumerable.Range(0, runIds.Count).Select(i => $"@{i}"));

        using var scope = _scopeProvider.CreateScope();
        var logs = scope.Database.Execute(
            $"DELETE FROM {BackgroundJobRunLogDto.TableName} WHERE {nameof(BackgroundJobRunLogDto.RunId)} IN ({placeholders})",
            parameters
        );
        var runs = scope.Database.Execute(
            $"DELETE FROM {BackgroundJobRunDto.TableName} WHERE {nameof(BackgroundJobRunDto.Id)} IN ({placeholders})",
            parameters
        );
        scope.Complete();
        return (logs, runs);
    }

    private static IEnumerable<IReadOnlyCollection<Guid>> Chunk(IReadOnlyList<Guid> items, int batchSize)
    {
        for (var offset = 0; offset < items.Count; offset += batchSize)
        {
            var take = Math.Min(batchSize, items.Count - offset);
            var batch = new Guid[take];
            for (var i = 0; i < take; i++)
            {
                batch[i] = items[offset + i];
            }

            yield return batch;
        }
    }
}

internal static class BackgroundJobRunRetentionPlanner
{
    public readonly record struct RunSummary(Guid Id, DateTime StartedAt);

    public static IEnumerable<Guid> SelectRunsToDelete(IReadOnlyList<RunSummary> runsNewestFirst, RunHistoryRetentionOptions options, DateTime now)
    {
        var toDelete = new HashSet<Guid>();

        if (options.MaxRunsPerJob > 0 && runsNewestFirst.Count > options.MaxRunsPerJob)
        {
            for (var i = options.MaxRunsPerJob; i < runsNewestFirst.Count; i++)
            {
                toDelete.Add(runsNewestFirst[i].Id);
            }
        }

        if (options.MaxAge > TimeSpan.Zero)
        {
            var cutoff = now - options.MaxAge;
            foreach (var run in runsNewestFirst)
            {
                if (run.StartedAt < cutoff)
                {
                    toDelete.Add(run.Id);
                }
            }
        }

        return toDelete;
    }
}
