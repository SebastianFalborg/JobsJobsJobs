using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace JobsJobsJobs.TestSite;

internal sealed class DashboardSmokeTestJob : IRecurringBackgroundJob
{
    private readonly ILogger<DashboardSmokeTestJob> _logger;
    private int _runCount;

    public DashboardSmokeTestJob(ILogger<DashboardSmokeTestJob> logger) => _logger = logger;

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

        await Task.Delay(TimeSpan.FromSeconds(10));

        if (runNumber % 2 == 0)
        {
            throw new InvalidOperationException("Intentional test failure from DashboardSmokeTestJob.");
        }

        _logger.LogInformation("DashboardSmokeTestJob run {RunNumber} completed", runNumber);
    }
}
