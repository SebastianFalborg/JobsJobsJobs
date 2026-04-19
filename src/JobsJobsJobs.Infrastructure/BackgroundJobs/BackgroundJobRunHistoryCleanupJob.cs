using JobsJobsJobs.Core.BackgroundJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobsJobsJobs.Infrastructure.BackgroundJobs;

internal sealed class BackgroundJobRunHistoryCleanupJob : RecurringBackgroundJobBase, IInternalBackgroundJob
{
    private static readonly TimeSpan s_minimumSweepInterval = TimeSpan.FromMinutes(1);

    private readonly ILogger<BackgroundJobRunHistoryCleanupJob> _logger;
    private readonly BackgroundJobDashboardOptions _options;
    private readonly IBackgroundJobRunRetentionService _retentionService;

    public BackgroundJobRunHistoryCleanupJob(
        ILogger<BackgroundJobRunHistoryCleanupJob> logger,
        IBackgroundJobRunRetentionService retentionService,
        IOptions<BackgroundJobDashboardOptions> options
    )
    {
        _logger = logger;
        _retentionService = retentionService;
        _options = options.Value;
    }

    public override TimeSpan Period
    {
        get
        {
            var configured = _options.RunHistoryRetention.SweepInterval;
            return configured < s_minimumSweepInterval ? s_minimumSweepInterval : configured;
        }
    }

    public override TimeSpan Delay => TimeSpan.FromMinutes(5);

    public override Task RunJobAsync()
    {
        if (_options.RunHistoryRetention.Enabled is false)
        {
            return Task.CompletedTask;
        }

        try
        {
            _retentionService.SweepOnce();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Background job run history cleanup failed. Will retry on next sweep.");
        }

        return Task.CompletedTask;
    }
}
