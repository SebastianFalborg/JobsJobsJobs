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

        try
        {
            var aliases = FetchDistinctAliases();

            foreach (var alias in aliases)
            {
                var runs = FetchRunsForAlias(alias);
                var idsToDelete = BackgroundJobRunRetentionPlanner.SelectRunsToDelete(runs, retention, now).ToArray();

                if (idsToDelete.Length == 0)
                {
                    continue;
                }

                foreach (var batch in Chunk(idsToDelete, batchSize))
                {
                    var (logs, runs2) = DeleteBatch(batch);
                    totalLogsDeleted += logs;
                    totalRunsDeleted += runs2;
                }
            }

            totalLogsDeleted += DeleteOrphanLogs(batchSize);

            if (totalRunsDeleted > 0 || totalLogsDeleted > 0)
            {
                _logger.LogInformation(
                    "Background job run history retention sweep completed. Deleted {RunCount} run rows and {LogCount} log rows.",
                    totalRunsDeleted,
                    totalLogsDeleted
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Background job run history retention sweep failed. Will retry on next interval.");
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
        var rows = scope.Database.Fetch<BackgroundJobRunDto>(
            $"SELECT * FROM {BackgroundJobRunDto.TableName} "
                + $"WHERE {nameof(BackgroundJobRunDto.JobAlias)} = @0 "
                + $"ORDER BY {nameof(BackgroundJobRunDto.StartedAt)} DESC",
            alias
        );
        scope.Complete();
        return rows.Select(r => new BackgroundJobRunRetentionPlanner.RunSummary(r.Id, r.StartedAt)).ToArray();
    }

    private (int LogsDeleted, int RunsDeleted) DeleteBatch(IReadOnlyCollection<Guid> runIds)
    {
        if (runIds.Count == 0)
        {
            return (0, 0);
        }

        var inClause = string.Join(",", runIds.Select(id => $"'{id}'"));
        using var scope = _scopeProvider.CreateScope();
        var logs = scope.Database.Execute(
            $"DELETE FROM {BackgroundJobRunLogDto.TableName} WHERE {nameof(BackgroundJobRunLogDto.RunId)} IN ({inClause})"
        );
        var runs = scope.Database.Execute($"DELETE FROM {BackgroundJobRunDto.TableName} WHERE {nameof(BackgroundJobRunDto.Id)} IN ({inClause})");
        scope.Complete();
        return (logs, runs);
    }

    private int DeleteOrphanLogs(int batchSize)
    {
        using var scope = _scopeProvider.CreateScope();
        var orphanIds = scope
            .Database.Fetch<Guid>(
                $"SELECT {nameof(BackgroundJobRunLogDto.Id)} FROM {BackgroundJobRunLogDto.TableName} "
                    + $"WHERE {nameof(BackgroundJobRunLogDto.RunId)} NOT IN (SELECT {nameof(BackgroundJobRunDto.Id)} FROM {BackgroundJobRunDto.TableName})"
            )
            .ToArray();
        scope.Complete();

        if (orphanIds.Length == 0)
        {
            return 0;
        }

        var totalDeleted = 0;
        foreach (var batch in Chunk(orphanIds, batchSize))
        {
            var inClause = string.Join(",", batch.Select(id => $"'{id}'"));
            using var deleteScope = _scopeProvider.CreateScope();
            totalDeleted += deleteScope.Database.Execute(
                $"DELETE FROM {BackgroundJobRunLogDto.TableName} WHERE {nameof(BackgroundJobRunLogDto.Id)} IN ({inClause})"
            );
            deleteScope.Complete();
        }

        return totalDeleted;
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
