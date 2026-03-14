# Jobs Jobs Jobs 
 
[![Downloads](https://img.shields.io/nuget/dt/Umbraco.Community.JobsJobsJobs?color=cc9900)](https://www.nuget.org/packages/Umbraco.Community.JobsJobsJobs/)
[![NuGet](https://img.shields.io/nuget/vpre/Umbraco.Community.JobsJobsJobs?color=0273B3)](https://www.nuget.org/packages/Umbraco.Community.JobsJobsJobs)
[![GitHub license](https://img.shields.io/github/license/NotSoap/JobsJobsJobs?color=8AB803)](https://github.com/NotSoap/JobsJobsJobs/blob/main/LICENSE)

Jobs Jobs Jobs adds a backoffice dashboard for recurring Umbraco background jobs, including runtime status and manual triggering.

## Install

Add the generated NuGet package to an Umbraco 17 site and restart the application.

## Register your first job

Create a recurring background job in your Umbraco app:

```csharp
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace MyUmbracoSite;

internal sealed class MyFirstBackgroundJob : IRecurringBackgroundJob
{
    private readonly ILogger<MyFirstBackgroundJob> _logger;

    public MyFirstBackgroundJob(ILogger<MyFirstBackgroundJob> logger) => _logger = logger;

    public TimeSpan Period => TimeSpan.FromMinutes(15);

    public TimeSpan Delay => TimeSpan.FromMinutes(1);

    public ServerRole[] ServerRoles => Enum.GetValues<ServerRole>();

    public event EventHandler? PeriodChanged
    {
        add { }
        remove { }
    }

    public Task RunJobAsync()
    {
        _logger.LogInformation("MyFirstBackgroundJob ran at {Time}", DateTime.UtcNow);
        return Task.CompletedTask;
    }
}
```

Register it in a composer:

```csharp
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Extensions;

namespace MyUmbracoSite;

internal sealed class MyBackgroundJobsComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddRecurringBackgroundJob<MyFirstBackgroundJob>();
    }
}
```

Restart the application and open `Settings -> Background Jobs` in the Umbraco backoffice.

## Persisted run logs

The package now stores the latest runs and log lines for each job in the database.

If you want your own job to write detailed run logs to the dashboard, inject `IBackgroundJobRunLogWriter<TJob>` into the job and write log lines while the job is running:

```csharp
using System;
using System.Threading.Tasks;
using JobsJobsJobs.BackgroundJobs;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace MyUmbracoSite;

internal sealed class MyLoggedBackgroundJob : IRecurringBackgroundJob
{
    private readonly ILogger<MyLoggedBackgroundJob> _logger;
    private readonly IBackgroundJobRunLogWriter<MyLoggedBackgroundJob> _logWriter;

    public MyLoggedBackgroundJob(
        ILogger<MyLoggedBackgroundJob> logger,
        IBackgroundJobRunLogWriter<MyLoggedBackgroundJob> logWriter)
    {
        _logger = logger;
        _logWriter = logWriter;
    }

    public TimeSpan Period => TimeSpan.FromMinutes(15);

    public TimeSpan Delay => TimeSpan.FromMinutes(1);

    public ServerRole[] ServerRoles => Enum.GetValues<ServerRole>();

    public event EventHandler? PeriodChanged
    {
        add { }
        remove { }
    }

    public Task RunJobAsync()
    {
        _logger.LogInformation("MyLoggedBackgroundJob started at {Time}", DateTime.UtcNow);
        _logWriter.Information("Starting import phase");
        _logWriter.Information("Imported 42 records");
        _logWriter.Warning("Skipped 2 invalid rows");
        _logWriter.Information("Finished cleanup phase");
        return Task.CompletedTask;
    }
}
```

You do not need to register `IBackgroundJobRunLogWriter<TJob>` yourself. The package registers it automatically.

The package also automatically:

- persists start/end/status for automatic job runs
- persists start/end/status for manual triggers from the dashboard
- stores log entries written with `IBackgroundJobRunLogWriter<TJob>` on the current active run
- exposes the latest stored run and log lines in the dashboard API and UI

The only thing you need to do in your own job is inject and use `IBackgroundJobRunLogWriter<TJob>`.

## Database tables

The package writes to these tables:

```text
JobsJobsJobsBackgroundJobRun
JobsJobsJobsBackgroundJobRunLog
```

`JobsJobsJobsBackgroundJobRun` stores one row per run.

`JobsJobsJobsBackgroundJobRunLog` stores the log lines for a run and links back via `RunId`.

## Check persisted runs in the database

In the test site, the default development database is SQLite:

```text
src/JobsJobsJobs.TestSite/umbraco/Data/Umbraco.sqlite.db
```

Example SQL:

```sql
select *
from JobsJobsJobsBackgroundJobRun
order by StartedAt desc;

select *
from JobsJobsJobsBackgroundJobRunLog
order by LoggedAt desc;

select r.Id,
       r.JobAlias,
       r.Status,
       r.StartedAt,
       r.CompletedAt,
       l.Level,
       l.Message,
       l.LoggedAt
from JobsJobsJobsBackgroundJobRun r
left join JobsJobsJobsBackgroundJobRunLog l on l.RunId = r.Id
order by r.StartedAt desc, l.LoggedAt asc;
```

## Use

Open the Umbraco backoffice and go to `Settings -> Background Jobs`.