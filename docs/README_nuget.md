# Jobs Jobs Jobs 
 
[![Downloads](https://img.shields.io/nuget/dt/Umbraco.Community.JobsJobsJobs?color=cc9900)](https://www.nuget.org/packages/Umbraco.Community.JobsJobsJobs/)
[![NuGet](https://img.shields.io/nuget/vpre/Umbraco.Community.JobsJobsJobs?color=0273B3)](https://www.nuget.org/packages/Umbraco.Community.JobsJobsJobs)
[![GitHub license](https://img.shields.io/github/license/SebastianFalborg/JobsJobsJobs?color=8AB803)](https://github.com/SebastianFalborg/JobsJobsJobs/blob/main/LICENSE)

Jobs Jobs Jobs adds a backoffice dashboard for recurring Umbraco background jobs, including runtime status and manual triggering.

## Why use this instead of Hangfire?

This package is for Umbraco solutions that already rely on `IRecurringBackgroundJob` and want a focused operational dashboard without adding a separate background processing platform.

- It stays close to Umbraco's own recurring job model
- It gives you status, manual triggering, persisted run history, and run logs in the backoffice
- It keeps the setup and operational model smaller than a full queue-processing solution

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

## Configuration

### Show Umbraco built-in jobs

By default, the dashboard hides Umbraco's own recurring jobs so the list stays focused on the jobs you added yourself.

If you want to include Umbraco jobs in the dashboard as well, configure the package options in a composer:

```csharp
using JobsJobsJobs.BackgroundJobs;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace MyUmbracoSite;

internal sealed class MyBackgroundJobsOptionsComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.Configure<BackgroundJobDashboardOptions>(options =>
        {
            options.IncludeUmbracoJobs = true;
        });
    }
}
```

Use this if you want one combined dashboard for both your own recurring jobs and Umbraco's built-in recurring jobs.

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

On application restart, the dashboard hydrates its summary columns from the latest persisted run if there is no newer in-memory state yet.

The only thing you need to do in your own job is inject and use `IBackgroundJobRunLogWriter<TJob>`.

## Dashboard behavior

### Filtering

The dashboard is intended to focus on your own jobs.

- Umbraco built-in recurring jobs are hidden by default
- You can opt in to showing them via `BackgroundJobDashboardOptions.IncludeUmbracoJobs`
- Your own jobs are still shown normally
- Filtering is applied consistently in the dashboard list, manual trigger endpoint, and persisted run history shown by the package

### Persistence

The package stores the latest runs in the database so the dashboard still has history after an application restart.

- Automatic runs are persisted
- Manual runs triggered from the dashboard are persisted
- Latest stored run details are shown even after restart
- Summary columns are hydrated from the latest stored run when live in-memory state is empty

### Manual triggers

The dashboard can trigger a registered job manually.

- Manual runs are blocked when the same job is already running
- Manual runs respect Umbraco runtime constraints such as MainDom and allowed server roles
- Manual runs are persisted in the same run history as automatic runs

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

## Non-goals and roadmap

This package is intentionally focused.

- It is not trying to become a general-purpose queue processing platform
- It is not trying to replicate Hangfire features like batches, continuations, or distributed worker orchestration
- It is meant to improve visibility and control for recurring Umbraco background jobs

Planned improvements should continue to focus on documentation, dashboard clarity, and operational insight rather than turning the package into a full workflow engine.