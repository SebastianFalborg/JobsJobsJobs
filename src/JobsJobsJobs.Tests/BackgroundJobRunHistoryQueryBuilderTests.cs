using JobsJobsJobs.Core.BackgroundJobs;
using JobsJobsJobs.Infrastructure.BackgroundJobs;
using Xunit;

namespace JobsJobsJobs.Tests;

public class BackgroundJobRunHistoryQueryBuilderTests
{
    [Fact]
    public void BuildWhereClause_WithEmptyQuery_ReturnsNoClause()
    {
        var (where, parameters) = BackgroundJobRunHistoryQueryBuilder.BuildWhereClause(new BackgroundJobRunHistoryQuery());

        Assert.Equal(string.Empty, where);
        Assert.Empty(parameters);
    }

    [Fact]
    public void BuildWhereClause_WithIncludeUmbracoFalse_ExcludesUmbracoAliases()
    {
        var (where, parameters) = BackgroundJobRunHistoryQueryBuilder.BuildWhereClause(new BackgroundJobRunHistoryQuery(), includeUmbracoJobs: false);

        Assert.Contains("JobAlias NOT LIKE @0", where);
        Assert.Single(parameters);
        Assert.Equal("Umbraco.%", parameters[0]);
    }

    [Fact]
    public void BuildWhereClause_WithIncludeUmbracoFalseAndTriggerFilter_CombinesBoth()
    {
        var (where, parameters) = BackgroundJobRunHistoryQueryBuilder.BuildWhereClause(
            new BackgroundJobRunHistoryQuery { Trigger = BackgroundJobRunTrigger.Manual },
            includeUmbracoJobs: false
        );

        Assert.Contains("JobAlias NOT LIKE @0", where);
        Assert.Contains("[Trigger] = @1", where);
        Assert.Contains("AND", where);
        Assert.Equal(2, parameters.Count);
        Assert.Equal("Umbraco.%", parameters[0]);
        Assert.Equal("Manual", parameters[1]);
    }

    [Fact]
    public void BuildWhereClause_WithJobAlias_EmitsAliasFilter()
    {
        var (where, parameters) = BackgroundJobRunHistoryQueryBuilder.BuildWhereClause(new BackgroundJobRunHistoryQuery { JobAlias = "My.Job" });

        Assert.Contains("JobAlias = @0", where);
        Assert.Single(parameters);
        Assert.Equal("My.Job", parameters[0]);
    }

    [Fact]
    public void BuildWhereClause_WithStatuses_EmitsInClause()
    {
        var (where, parameters) = BackgroundJobRunHistoryQueryBuilder.BuildWhereClause(
            new BackgroundJobRunHistoryQuery { Statuses = new[] { BackgroundJobStatus.Failed, BackgroundJobStatus.Stopped } }
        );

        Assert.Contains("Status IN (@0,@1)", where);
        Assert.Equal(2, parameters.Count);
        Assert.Equal("Failed", parameters[0]);
        Assert.Equal("Stopped", parameters[1]);
    }

    [Fact]
    public void BuildWhereClause_WithTrigger_EmitsTriggerFilter()
    {
        var (where, parameters) = BackgroundJobRunHistoryQueryBuilder.BuildWhereClause(
            new BackgroundJobRunHistoryQuery { Trigger = BackgroundJobRunTrigger.Manual }
        );

        Assert.Contains("[Trigger] = @0", where);
        Assert.Single(parameters);
        Assert.Equal("Manual", parameters[0]);
    }

    [Fact]
    public void BuildWhereClause_WithStartedAfter_EmitsLowerBound()
    {
        var after = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var (where, parameters) = BackgroundJobRunHistoryQueryBuilder.BuildWhereClause(new BackgroundJobRunHistoryQuery { StartedAfter = after });

        Assert.Contains("StartedAt >= @0", where);
        Assert.Single(parameters);
        Assert.Equal(after, parameters[0]);
    }

    [Fact]
    public void BuildWhereClause_WithStartedBefore_EmitsUpperBound()
    {
        var before = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var (where, parameters) = BackgroundJobRunHistoryQueryBuilder.BuildWhereClause(new BackgroundJobRunHistoryQuery { StartedBefore = before });

        Assert.Contains("StartedAt <= @0", where);
        Assert.Single(parameters);
        Assert.Equal(before, parameters[0]);
    }

    [Fact]
    public void BuildWhereClause_WithSearch_EmitsLikeAndExists()
    {
        var (where, parameters) = BackgroundJobRunHistoryQueryBuilder.BuildWhereClause(new BackgroundJobRunHistoryQuery { Search = "boom" });

        Assert.Contains("Error LIKE @0", where);
        Assert.Contains("Message LIKE @0", where);
        Assert.Contains("EXISTS", where);
        Assert.Single(parameters);
        Assert.Equal("%boom%", parameters[0]);
    }

    [Fact]
    public void BuildWhereClause_WithSearchWhitespaceOnly_IsIgnored()
    {
        var (where, parameters) = BackgroundJobRunHistoryQueryBuilder.BuildWhereClause(new BackgroundJobRunHistoryQuery { Search = "   " });

        Assert.Equal(string.Empty, where);
        Assert.Empty(parameters);
    }

    [Fact]
    public void BuildWhereClause_WithAllFilters_CombinesWithAnd()
    {
        var after = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var before = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var (where, parameters) = BackgroundJobRunHistoryQueryBuilder.BuildWhereClause(
            new BackgroundJobRunHistoryQuery
            {
                JobAlias = "My.Job",
                Statuses = new[] { BackgroundJobStatus.Failed },
                Trigger = BackgroundJobRunTrigger.Automatic,
                StartedAfter = after,
                StartedBefore = before,
                Search = "err",
            }
        );

        Assert.StartsWith("WHERE ", where);
        Assert.Contains("AND", where);
        Assert.Equal(6, parameters.Count);
        Assert.Equal("My.Job", parameters[0]);
        Assert.Equal("Failed", parameters[1]);
        Assert.Equal("Automatic", parameters[2]);
        Assert.Equal(after, parameters[3]);
        Assert.Equal(before, parameters[4]);
        Assert.Equal("%err%", parameters[5]);
    }

    [Fact]
    public void BuildWhereClause_WithEmptyJobAlias_IsIgnored()
    {
        var (where, parameters) = BackgroundJobRunHistoryQueryBuilder.BuildWhereClause(new BackgroundJobRunHistoryQuery { JobAlias = "" });

        Assert.Equal(string.Empty, where);
        Assert.Empty(parameters);
    }

    [Fact]
    public void BuildWhereClause_WithEmptyStatuses_IsIgnored()
    {
        var (where, parameters) = BackgroundJobRunHistoryQueryBuilder.BuildWhereClause(
            new BackgroundJobRunHistoryQuery { Statuses = Array.Empty<BackgroundJobStatus>() }
        );

        Assert.Equal(string.Empty, where);
        Assert.Empty(parameters);
    }
}
