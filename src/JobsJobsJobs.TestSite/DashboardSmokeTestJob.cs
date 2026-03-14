using System;
using System.Threading;
using System.Threading.Tasks;
using JobsJobsJobs.BackgroundJobs;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace JobsJobsJobs.TestSite;

internal sealed class DashboardSmokeTestJob : IRecurringBackgroundJob
{
    private readonly ILogger<DashboardSmokeTestJob> _logger;
    private readonly IBackgroundJobRunLogWriter<DashboardSmokeTestJob> _runLogWriter;
    private int _runCount;

    public DashboardSmokeTestJob(
        ILogger<DashboardSmokeTestJob> logger,
        IBackgroundJobRunLogWriter<DashboardSmokeTestJob> runLogWriter)
    {
        _logger = logger;
        _runLogWriter = runLogWriter;
    }

    public TimeSpan Period => TimeSpan.FromDays(1);

    public TimeSpan Delay => TimeSpan.FromDays(1);

    public ServerRole[] ServerRoles => Enum.GetValues<ServerRole>();

    public event EventHandler? PeriodChanged
    {
        add { }
        remove { }
    }

    public async Task RunJobAsync()
    {
        var runNumber = Interlocked.Increment(ref _runCount);

        _logger.LogInformation("DashboardSmokeTestJob run {RunNumber} started", runNumber);
        _runLogWriter.Information($"Run {runNumber} started.");
        _runLogWriter.Information("Waiting 10 seconds to simulate work.");

        await Task.Delay(TimeSpan.FromSeconds(10));

        if (runNumber % 2 == 0)
        {
            _runLogWriter.Warning("This run is configured to fail intentionally.");
            _runLogWriter.Error("Throwing intentional test failure.");
            throw new InvalidOperationException("Intentional test failure from DashboardSmokeTestJob.");
        }

        _runLogWriter.Information("Run completed successfully.");
        _logger.LogInformation("DashboardSmokeTestJob run {RunNumber} completed", runNumber);
    }
}
