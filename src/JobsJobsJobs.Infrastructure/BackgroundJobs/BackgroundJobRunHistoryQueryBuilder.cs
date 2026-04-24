using JobsJobsJobs.Core.BackgroundJobs;

namespace JobsJobsJobs.Infrastructure.BackgroundJobs;

internal static class BackgroundJobRunHistoryQueryBuilder
{
    public const int MinimumSearchTermLength = 3;

    public static (string WhereClause, List<object> Parameters) BuildWhereClause(BackgroundJobRunHistoryQuery query, bool includeUmbracoJobs = true)
    {
        var clauses = new List<string>();
        var parameters = new List<object>();

        if (includeUmbracoJobs is false)
        {
            clauses.Add($"r.{nameof(BackgroundJobRunDto.JobAlias)} NOT LIKE @{parameters.Count}");
            parameters.Add("Umbraco.%");
        }

        if (string.IsNullOrWhiteSpace(query.JobAlias) is false)
        {
            clauses.Add($"r.{nameof(BackgroundJobRunDto.JobAlias)} = @{parameters.Count}");
            parameters.Add(query.JobAlias);
        }

        if (query.Statuses.Count > 0)
        {
            var placeholders = query
                .Statuses.Select(status =>
                {
                    parameters.Add(status.ToString());
                    return $"@{parameters.Count - 1}";
                })
                .ToArray();
            clauses.Add($"r.{nameof(BackgroundJobRunDto.Status)} IN ({string.Join(",", placeholders)})");
        }

        if (query.Trigger.HasValue)
        {
            clauses.Add($"r.[{nameof(BackgroundJobRunDto.Trigger)}] = @{parameters.Count}");
            parameters.Add(query.Trigger.Value.ToString());
        }

        if (query.StartedAfter.HasValue)
        {
            clauses.Add($"r.{nameof(BackgroundJobRunDto.StartedAt)} >= @{parameters.Count}");
            parameters.Add(query.StartedAfter.Value);
        }

        if (query.StartedBefore.HasValue)
        {
            clauses.Add($"r.{nameof(BackgroundJobRunDto.StartedAt)} <= @{parameters.Count}");
            parameters.Add(query.StartedBefore.Value);
        }

        var trimmedSearch = query.Search?.Trim() ?? string.Empty;
        if (trimmedSearch.Length >= MinimumSearchTermLength)
        {
            var pattern = "%" + trimmedSearch + "%";
            var searchIndex = parameters.Count;
            parameters.Add(pattern);

            clauses.Add(
                $"(r.{nameof(BackgroundJobRunDto.Error)} LIKE @{searchIndex} "
                    + $"OR r.{nameof(BackgroundJobRunDto.Message)} LIKE @{searchIndex} "
                    + $"OR EXISTS (SELECT 1 FROM {BackgroundJobRunLogDto.TableName} l "
                    + $"WHERE l.{nameof(BackgroundJobRunLogDto.RunId)} = r.{nameof(BackgroundJobRunDto.Id)} "
                    + $"AND l.{nameof(BackgroundJobRunLogDto.Message)} LIKE @{searchIndex}))"
            );
        }

        var where = clauses.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", clauses);
        return (where, parameters);
    }
}
