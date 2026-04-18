using JobsJobsJobs.BackgroundJobs;
using JobsJobsJobs.Core.BackgroundJobs;
using Xunit;

namespace JobsJobsJobs.Tests;

public class BackgroundJobCronSchedulerTests
{
    [Fact]
    public void ShouldExecute_WhenFirstOccurrenceInWindowIsDue_ReturnsTrue()
    {
        DateTime startedAtUtc = new(2026, 3, 15, 22, 0, 5, DateTimeKind.Utc);
        var scheduler = CreateScheduler(startedAtUtc.AddMinutes(-1));

        var result = scheduler.ShouldExecute("Sunday2200UtcCronTestJob", "* 22-23 * * SUN", TimeZoneInfo.Utc, startedAtUtc);

        Assert.True(result);
    }

    [Fact]
    public void ShouldExecute_WhenBeforeSecondWindow_ReturnsFalse()
    {
        DateTime startedAtUtc = new(2026, 3, 15, 22, 11, 5, DateTimeKind.Utc);
        var scheduler = CreateScheduler(startedAtUtc.AddMinutes(-1));

        var result = scheduler.ShouldExecute("Sunday2230UtcCronTestJob", "30-59 22 * * SUN; * 23 * * SUN", TimeZoneInfo.Utc, startedAtUtc);

        Assert.False(result);
    }

    [Fact]
    public void ShouldExecute_WhenSecondWindowStarts_ReturnsTrue()
    {
        DateTime startedAtUtc = new(2026, 3, 15, 22, 30, 5, DateTimeKind.Utc);
        var scheduler = CreateScheduler(startedAtUtc.AddMinutes(-1));

        var result = scheduler.ShouldExecute("Sunday2230UtcCronTestJob", "30-59 22 * * SUN; * 23 * * SUN", TimeZoneInfo.Utc, startedAtUtc);

        Assert.True(result);
    }

    [Fact]
    public void ShouldExecute_WhenLatestAutomaticRunAlreadyCoveredCurrentMinute_ReturnsFalse()
    {
        DateTime startedAtUtc = new(2026, 3, 15, 22, 30, 55, DateTimeKind.Utc);
        var latestRun = new BackgroundJobRunHistoryItem
        {
            JobAlias = "Sunday2200UtcCronTestJob",
            StartedAt = new DateTime(startedAtUtc.Year, startedAtUtc.Month, startedAtUtc.Day, 22, 30, 5, DateTimeKind.Utc),
        };
        var scheduler = CreateScheduler(startedAtUtc.AddHours(-1), latestRun);

        var result = scheduler.ShouldExecute("Sunday2200UtcCronTestJob", "* 22-23 * * SUN", TimeZoneInfo.Utc, startedAtUtc);

        Assert.False(result);
    }

    [Fact]
    public void ShouldExecute_WhenTimesAreUnspecified_NormalizesToUtc()
    {
        DateTime sundayUtc = new(2026, 3, 15, 22, 30, 5, DateTimeKind.Utc);
        var latestRun = new BackgroundJobRunHistoryItem
        {
            JobAlias = "Sunday2200UtcCronTestJob",
            StartedAt = new DateTime(sundayUtc.Year, sundayUtc.Month, sundayUtc.Day, 22, 29, 5, DateTimeKind.Unspecified),
        };
        var scheduler = CreateScheduler(sundayUtc.AddHours(-1), latestRun);
        var startedAtUtc = new DateTime(sundayUtc.Year, sundayUtc.Month, sundayUtc.Day, 22, 30, 5, DateTimeKind.Unspecified);

        var result = scheduler.ShouldExecute("Sunday2200UtcCronTestJob", "* 22-23 * * SUN", TimeZoneInfo.Utc, startedAtUtc);

        Assert.True(result);
    }

    [Fact]
    public void ShouldExecute_CachesBaselineAcrossCalls_AndReadsHistoryOnlyOnce()
    {
        DateTime startedAtUtc = new(2026, 3, 15, 22, 0, 5, DateTimeKind.Utc);
        var history = new StubRunHistoryService();
        var scheduler = new BackgroundJobCronScheduler(history, () => startedAtUtc.AddMinutes(-1));

        scheduler.ShouldExecute("Sunday2200UtcCronTestJob", "* 22-23 * * SUN", TimeZoneInfo.Utc, startedAtUtc);
        scheduler.ShouldExecute("Sunday2200UtcCronTestJob", "* 22-23 * * SUN", TimeZoneInfo.Utc, startedAtUtc.AddSeconds(30));
        scheduler.ShouldExecute("Sunday2200UtcCronTestJob", "* 22-23 * * SUN", TimeZoneInfo.Utc, startedAtUtc.AddSeconds(60));

        Assert.Equal(1, history.GetLatestRunsCallCount);
    }

    [Fact]
    public void ShouldExecute_WhenReturnsTrue_UpdatesBaselineSoNextEvaluationDoesNotRefire()
    {
        DateTime firstEvaluation = new(2026, 3, 15, 22, 0, 5, DateTimeKind.Utc);
        var scheduler = CreateScheduler(firstEvaluation.AddMinutes(-1));

        var first = scheduler.ShouldExecute("Sunday2200UtcCronTestJob", "0 22 * * SUN", TimeZoneInfo.Utc, firstEvaluation);
        var second = scheduler.ShouldExecute("Sunday2200UtcCronTestJob", "0 22 * * SUN", TimeZoneInfo.Utc, firstEvaluation.AddSeconds(30));

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public void ShouldExecute_WhenTriggerIsManual_AdapterAlwaysAllowsExecution()
    {
        var adapter = new CronRecurringBackgroundJobAdapter<TestCronJob>(
            new TestCronJob(),
            CreateScheduler(new DateTime(2026, 3, 15, 22, 0, 0, DateTimeKind.Utc)),
            new BackgroundJobCronSuppressionCoordinator(),
            new BackgroundJobRunExecutionContextAccessor()
        );
        DateTime startedAtUtc = new(2026, 3, 15, 22, 11, 5, DateTimeKind.Utc);
        var context = new BackgroundJobRunExecutionContext
        {
            Trigger = BackgroundJobRunTrigger.Manual,
            StartedAt = startedAtUtc,
            JobAlias = "TestCronJob",
        };

        var result = adapter.ShouldExecute(context);

        Assert.True(result);
    }

    private static BackgroundJobCronScheduler CreateScheduler(DateTime startedAtUtc, params BackgroundJobRunHistoryItem[] runs) =>
        new(new StubRunHistoryService(runs), () => startedAtUtc);

    private sealed class StubRunHistoryService : IBackgroundJobRunHistoryService
    {
        private readonly IReadOnlyDictionary<string, BackgroundJobRunHistoryItem> _runs;

        public StubRunHistoryService(params BackgroundJobRunHistoryItem[] runs) =>
            _runs = runs.ToDictionary(x => x.JobAlias, StringComparer.OrdinalIgnoreCase);

        public int GetLatestRunsCallCount { get; private set; }

        public IReadOnlyDictionary<string, BackgroundJobRunHistoryItem> GetLatestRuns(
            IEnumerable<string> aliases,
            BackgroundJobRunTrigger? trigger = null,
            int maxLogsPerRun = 20
        )
        {
            GetLatestRunsCallCount++;
            return aliases
                .Where(alias => _runs.ContainsKey(alias))
                .ToDictionary(alias => alias, alias => _runs[alias], StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyDictionary<string, IReadOnlyCollection<BackgroundJobRunHistoryItem>> GetRecentRuns(
            IEnumerable<string> aliases,
            int maxRuns = 5,
            int maxLogsPerRun = 0
        ) =>
            aliases
                .Where(alias => _runs.ContainsKey(alias))
                .ToDictionary(
                    alias => alias,
                    alias => (IReadOnlyCollection<BackgroundJobRunHistoryItem>)new[] { _runs[alias] },
                    StringComparer.OrdinalIgnoreCase
                );
    }

    private sealed class TestCronJob : CronBackgroundJobBase
    {
        public override string CronExpression => "30-59 22 * * SUN; * 23 * * SUN";

        public override Task RunJobAsync() => Task.CompletedTask;
    }
}
