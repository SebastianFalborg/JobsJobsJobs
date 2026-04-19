using JobsJobsJobs.Core.BackgroundJobs;
using JobsJobsJobs.Infrastructure.BackgroundJobs;
using Xunit;

namespace JobsJobsJobs.Tests;

public class BackgroundJobRunRetentionPlannerTests
{
    private static readonly DateTime s_now = new(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void SelectRunsToDelete_WhenRunsFitWithinBothLimits_ReturnsEmpty()
    {
        var runs = BuildRuns(count: 5, stepBack: TimeSpan.FromMinutes(1));
        var options = new RunHistoryRetentionOptions { MaxRunsPerJob = 10, MaxAge = TimeSpan.FromDays(30) };

        var result = BackgroundJobRunRetentionPlanner.SelectRunsToDelete(runs, options, s_now);

        Assert.Empty(result);
    }

    [Fact]
    public void SelectRunsToDelete_WhenCountExceedsMaxRunsPerJob_ReturnsOldestExcess()
    {
        var runs = BuildRuns(count: 10, stepBack: TimeSpan.FromMinutes(1));
        var options = new RunHistoryRetentionOptions { MaxRunsPerJob = 3, MaxAge = TimeSpan.FromDays(30) };

        var result = BackgroundJobRunRetentionPlanner.SelectRunsToDelete(runs, options, s_now).ToArray();

        Assert.Equal(7, result.Length);
        var expected = runs.Skip(3).Select(r => r.Id).ToArray();
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SelectRunsToDelete_WhenRunsExceedMaxAge_ReturnsOldOnes()
    {
        var runs = new List<BackgroundJobRunRetentionPlanner.RunSummary>
        {
            new(Guid.NewGuid(), s_now.AddMinutes(-1)),
            new(Guid.NewGuid(), s_now.AddHours(-2)),
            new(Guid.NewGuid(), s_now.AddDays(-2)),
            new(Guid.NewGuid(), s_now.AddDays(-40)),
            new(Guid.NewGuid(), s_now.AddDays(-100)),
        };
        var options = new RunHistoryRetentionOptions { MaxRunsPerJob = 0, MaxAge = TimeSpan.FromDays(30) };

        var result = BackgroundJobRunRetentionPlanner.SelectRunsToDelete(runs, options, s_now).ToArray();

        Assert.Equal(2, result.Length);
        Assert.Contains(runs[3].Id, result);
        Assert.Contains(runs[4].Id, result);
    }

    [Fact]
    public void SelectRunsToDelete_WhenBothRulesApply_ReturnsUnion()
    {
        var runs = new List<BackgroundJobRunRetentionPlanner.RunSummary>
        {
            new(Guid.NewGuid(), s_now.AddMinutes(-1)),
            new(Guid.NewGuid(), s_now.AddMinutes(-2)),
            new(Guid.NewGuid(), s_now.AddMinutes(-3)),
            new(Guid.NewGuid(), s_now.AddMinutes(-4)),
            new(Guid.NewGuid(), s_now.AddDays(-50)),
        };
        var options = new RunHistoryRetentionOptions { MaxRunsPerJob = 2, MaxAge = TimeSpan.FromDays(30) };

        var result = BackgroundJobRunRetentionPlanner.SelectRunsToDelete(runs, options, s_now).ToArray();

        Assert.Equal(3, result.Length);
        Assert.Contains(runs[2].Id, result);
        Assert.Contains(runs[3].Id, result);
        Assert.Contains(runs[4].Id, result);
    }

    [Fact]
    public void SelectRunsToDelete_WhenMaxRunsPerJobIsZero_OnlyAgeRuleApplies()
    {
        var runs = BuildRuns(count: 500, stepBack: TimeSpan.FromSeconds(1));
        var options = new RunHistoryRetentionOptions { MaxRunsPerJob = 0, MaxAge = TimeSpan.FromDays(30) };

        var result = BackgroundJobRunRetentionPlanner.SelectRunsToDelete(runs, options, s_now);

        Assert.Empty(result);
    }

    [Fact]
    public void SelectRunsToDelete_WhenMaxAgeIsZero_OnlyCountRuleApplies()
    {
        var runs = new List<BackgroundJobRunRetentionPlanner.RunSummary>
        {
            new(Guid.NewGuid(), s_now.AddYears(-10)),
            new(Guid.NewGuid(), s_now.AddYears(-9)),
            new(Guid.NewGuid(), s_now.AddYears(-8)),
        };
        var options = new RunHistoryRetentionOptions { MaxRunsPerJob = 5, MaxAge = TimeSpan.Zero };

        var result = BackgroundJobRunRetentionPlanner.SelectRunsToDelete(runs, options, s_now);

        Assert.Empty(result);
    }

    [Fact]
    public void SelectRunsToDelete_WhenBothDisabled_ReturnsEmpty()
    {
        var runs = BuildRuns(count: 10_000, stepBack: TimeSpan.FromDays(1));
        var options = new RunHistoryRetentionOptions { MaxRunsPerJob = 0, MaxAge = TimeSpan.Zero };

        var result = BackgroundJobRunRetentionPlanner.SelectRunsToDelete(runs, options, s_now);

        Assert.Empty(result);
    }

    [Fact]
    public void SelectRunsToDelete_WhenResultOverlaps_DoesNotDuplicateIds()
    {
        var shared = new BackgroundJobRunRetentionPlanner.RunSummary(Guid.NewGuid(), s_now.AddDays(-50));
        var runs = new List<BackgroundJobRunRetentionPlanner.RunSummary> { new(Guid.NewGuid(), s_now.AddMinutes(-1)), shared };
        var options = new RunHistoryRetentionOptions { MaxRunsPerJob = 1, MaxAge = TimeSpan.FromDays(30) };

        var result = BackgroundJobRunRetentionPlanner.SelectRunsToDelete(runs, options, s_now).ToArray();

        Assert.Single(result);
        Assert.Equal(shared.Id, result[0]);
    }

    private static List<BackgroundJobRunRetentionPlanner.RunSummary> BuildRuns(int count, TimeSpan stepBack)
    {
        var runs = new List<BackgroundJobRunRetentionPlanner.RunSummary>(count);
        for (var i = 0; i < count; i++)
        {
            runs.Add(new BackgroundJobRunRetentionPlanner.RunSummary(Guid.NewGuid(), s_now - TimeSpan.FromTicks(stepBack.Ticks * i)));
        }

        return runs;
    }
}
