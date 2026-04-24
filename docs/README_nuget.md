# Jobs Jobs Jobs

[![Downloads](https://img.shields.io/nuget/dt/Umbraco.Community.JobsJobsJobs?color=cc9900)](https://www.nuget.org/packages/Umbraco.Community.JobsJobsJobs/)
[![NuGet](https://img.shields.io/nuget/vpre/Umbraco.Community.JobsJobsJobs?color=0273B3)](https://www.nuget.org/packages/Umbraco.Community.JobsJobsJobs)
[![GitHub license](https://img.shields.io/github/license/SebastianFalborg/JobsJobsJobs?color=8AB803)](https://github.com/SebastianFalborg/JobsJobsJobs/blob/main/LICENSE)

Jobs Jobs Jobs is a simple job scheduler and backoffice dashboard for Umbraco, built on top of Umbraco's existing background jobs infrastructure. It adds runtime status, manual triggering, persisted run history, stored run logs, cooperative stop support, and opt-in CRON scheduling.

![Jobs Jobs Jobs dashboard — overview](https://raw.githubusercontent.com/SebastianFalborg/JobsJobsJobs/main/docs/Screenshots/Dashboard%20overview.png)

This package is available on the Umbraco Marketplace, but it has not been tested on production workloads yet. Please do your own testing and validation before using it in production.

## Why use this instead of Hangfire?

This package is for Umbraco projects that want a simpler, more Umbraco-native alternative to external schedulers when the goal is scheduled jobs with visibility and control inside the Umbraco backoffice.

- It is built on top of Umbraco's own background jobs model
- It gives you status, manual triggering, persisted run history, stored run logs, and stop support in the backoffice
- It keeps the setup and operational model smaller than a full queue-processing solution
- It is a good fit when you want scheduled jobs inside Umbraco instead of introducing an external job platform

## Install

Add the generated NuGet package to an Umbraco 17 site and restart the application.

## Choose your job type

Pick the base class that matches how the job should be scheduled:

- use `RecurringBackgroundJobBase` for normal recurring jobs driven by `Period` and `Delay`
- use `CronBackgroundJobBase` for jobs driven by a CRON expression

If the job should support cooperative stop requests from the dashboard, also implement the matching stoppable interface:

- `IStoppableRecurringBackgroundJob`
- `IStoppableCronBackgroundJob`

## Register your first recurring job

Create a recurring background job in your Umbraco app:

```csharp
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace MyUmbracoSite;

internal sealed class MyFirstBackgroundJob : RecurringBackgroundJobBase
{
    private readonly ILogger<MyFirstBackgroundJob> _logger;

    public MyFirstBackgroundJob(ILogger<MyFirstBackgroundJob> logger) => _logger = logger;

    public override TimeSpan Period => TimeSpan.FromMinutes(15);

    public override TimeSpan Delay => TimeSpan.FromMinutes(1);

    public override Task RunJobAsync()
    {
        _logger.LogInformation("MyFirstBackgroundJob ran at {Time}", DateTime.UtcNow);
        return Task.CompletedTask;
    }
}
```

Register it in a composer:

```csharp
using JobsJobsJobs.Infrastructure.BackgroundJobs;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace MyUmbracoSite;

internal sealed class MyBackgroundJobsComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddRecurringBackgroundJob<MyFirstBackgroundJob>();
    }
}
```

Restart the application and open `Settings -> Background Jobs` in the Umbraco backoffice.

## CRON schedules

If `Period` and `Delay` are too limited for a job, you can opt in to CRON scheduling.

CRON jobs are registered through Jobs Jobs Jobs, but they still run on top of Umbraco's recurring job infrastructure.

- use `ICronBackgroundJob` or `CronBackgroundJobBase` when you want CRON semantics
- use `IStoppableCronBackgroundJob` on a `CronBackgroundJobBase` job if the job should also support cooperative stop requests
- register CRON jobs with `builder.AddCronBackgroundJob<TJob>()`

`AddCronBackgroundJob<TJob>()` is an extension method on `IUmbracoBuilder`.

Make sure the composer imports:

- `JobsJobsJobs.Infrastructure.BackgroundJobs`

Otherwise you can get `CS1061: 'IUmbracoBuilder' does not contain a definition for 'AddCronBackgroundJob'` even when the package is installed correctly.

```csharp
using System;
using System.Threading.Tasks;
using JobsJobsJobs.Core.BackgroundJobs;
using Umbraco.Cms.Core.Sync;

namespace MyUmbracoSite;

internal sealed class MyCronBackgroundJob : CronBackgroundJobBase
{
    public override string CronExpression => "0 2 * * *";

    public override TimeZoneInfo TimeZone => TimeZoneInfo.Utc;

    public override ServerRole[] ServerRoles => Enum.GetValues<ServerRole>();

    public override Task RunJobAsync()
    {
        return Task.CompletedTask;
    }
}
```

Register it in a composer:

```csharp
using JobsJobsJobs.Infrastructure.BackgroundJobs;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace MyUmbracoSite;

internal sealed class MyCronBackgroundJobsComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddCronBackgroundJob<MyCronBackgroundJob>();
    }
}
```

If your job should also support stop requests, implement `IStoppableCronBackgroundJob` on a `CronBackgroundJobBase` job and still register it with `AddCronBackgroundJob<TJob>()`:

```csharp
using JobsJobsJobs.Infrastructure.BackgroundJobs;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace MyUmbracoSite;

internal sealed class MyStoppableCronBackgroundJobsComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddCronBackgroundJob<MyStoppableCronBackgroundJob>();
    }
}
```

If you prefer, the package still also exposes `AddStoppableCronBackgroundJob<TJob>()` and `IServiceCollection` overloads. The builder-based `AddCronBackgroundJob<TJob>()` API is the recommended happy path in Umbraco composers.

How CRON relates to `Period` and `Delay`:

- CRON support is opt in and does not change existing `IRecurringBackgroundJob` implementations
- the CRON expression is the schedule for the job body
- the optional time zone controls how that CRON expression is interpreted
- internally, Jobs Jobs Jobs polls with a recurring Umbraco job and only executes the job body when the next CRON occurrence is due
- `PollingPeriod` controls how often the CRON expression is checked
- `Delay` controls the initial delay before the polling loop starts
- the dashboard shows the CRON expression as the schedule instead of the internal polling period
- manual `Run now` still runs the job immediately and does not wait for the next CRON occurrence

The default polling period for `CronBackgroundJobBase` is one minute. You can override `PollingPeriod` and `Delay` if you need a different polling cadence.

If you use a CRON job, do not also register the same logical job as a normal `IRecurringBackgroundJob`. Pick one scheduling model per job.

For example, a CRON job with `CronExpression = "* 22-23 * * SUN"` and `PollingPeriod = TimeSpan.FromMinutes(1)` means:

- Umbraco checks every minute
- the job body is only allowed to run on Sundays between `22:00` and `23:59`

If you need multiple CRON windows for one job, separate expressions with `;`.

For example, Sunday `22:30-23:59` UTC can be expressed as:

```csharp
public override string CronExpression => "30-59 22 * * SUN; * 23 * * SUN";
```

## Configuration

### Show Umbraco built-in jobs

By default, the dashboard hides Umbraco's own recurring jobs so the list stays focused on the jobs you added yourself.

If you want to include Umbraco jobs in the dashboard as well, configure the package options in a composer:

```csharp
using JobsJobsJobs.Core.BackgroundJobs;
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
using JobsJobsJobs.Core.BackgroundJobs;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Sync;

namespace MyUmbracoSite;

internal sealed class MyLoggedBackgroundJob : RecurringBackgroundJobBase
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

    public override TimeSpan Period => TimeSpan.FromMinutes(15);

    public override TimeSpan Delay => TimeSpan.FromMinutes(1);

    public override Task RunJobAsync()
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

## Stop support and cooperative cancellation

The dashboard can request that a running job stops, but jobs are stopped cooperatively rather than forcefully.

![Stoppable CRON job card showing cooperative cancellation in the dashboard](https://raw.githubusercontent.com/SebastianFalborg/JobsJobsJobs/main/docs/Screenshots/Stoppable%20CRON%20job.png)

The recommended pattern is:

- choose `RecurringBackgroundJobBase` or `CronBackgroundJobBase` first
- add `IStoppableRecurringBackgroundJob` or `IStoppableCronBackgroundJob` if the job should support stop
- inject `IBackgroundJobExecutionCancellation` and observe it inside the job body

For a stoppable recurring job, implement `IStoppableRecurringBackgroundJob` on top of `RecurringBackgroundJobBase` and inject `IBackgroundJobExecutionCancellation` into the job.

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using JobsJobsJobs.Core.BackgroundJobs;
using Umbraco.Cms.Core.Sync;

namespace MyUmbracoSite;

internal sealed class MyStoppableBackgroundJob : RecurringBackgroundJobBase, IStoppableRecurringBackgroundJob
{
    private readonly IBackgroundJobExecutionCancellation _executionCancellation;

    public MyStoppableBackgroundJob(IBackgroundJobExecutionCancellation executionCancellation)
        => _executionCancellation = executionCancellation;

    public override TimeSpan Period => TimeSpan.FromMinutes(15);

    public override TimeSpan Delay => TimeSpan.FromMinutes(1);

    public override async Task RunJobAsync()
    {
        for (var i = 0; i < 10; i++)
        {
            _executionCancellation.ThrowIfCancellationRequested(this);
            await Task.Delay(TimeSpan.FromSeconds(1), _executionCancellation.GetCancellationToken(this));
        }
    }
}
```

Best practices for stoppable jobs:

- pass `_executionCancellation.GetCancellationToken(this)` to long-running async operations such as `Task.Delay`, HTTP calls, and I/O where possible
- call `_executionCancellation.ThrowIfCancellationRequested(this)` between meaningful work units in loops or multi-step workflows
- use `try/catch (OperationCanceledException)` if you want to write a final log entry or perform shutdown-specific cleanup before the job exits
- use `finally` for cleanup that must happen whether the job succeeds, fails, or is stopped
- do not treat stop as a hard kill; if the job never checks the cancellation signal, it will keep running until its own code finishes

If a recurring job does not implement `IStoppableRecurringBackgroundJob`, the dashboard will not show a stop action for that job.

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

### Stop requests

The dashboard can request stop for running jobs that explicitly opt in to cooperative cancellation.

- Stop is only shown for jobs implementing `IStoppableRecurringBackgroundJob`
- Stop is cooperative and depends on the job observing `IBackgroundJobExecutionCancellation`
- Jobs should check for stop between meaningful work units and pass the cancellation token into cancellable async operations
- Use `OperationCanceledException` handling only when you need extra cleanup or logging inside the job itself

### Viewing logs for a run

Each row in the `Recent stored runs` panel has a `Show logs` toggle. Expanding it fetches every log line captured for that specific run and lists them with timestamps and level badges. Logs are fetched on demand so opening the toggle does not affect dashboard load time.

### Resilience to server errors

The dashboard auto-refreshes every 5 seconds. If the server returns a 5xx response three times in a row, auto-refresh is paused and a persistent banner appears with a `Retry` button next to the error message. Clicking `Retry` resets the counter and resumes auto-refresh on the next successful response.

> **UX change in 1.6.0.** The global `Refresh` button has been removed from the dashboard toolbar. The 5 s auto-refresh on the Jobs tab already covers the common case, and the new History tab has its own `Search` button for intentional reloads, so one toolbar-level action covered two different behaviours and tended to confuse. Auto-refresh is also paused while the History tab is active so filters and pagination are not reset underneath you.

### History tab

The dashboard has two tabs: `Jobs` (the live status view) and `History` (a cross-job browser over every persisted run in the retention-capped history).

![History tab with filters and paginated run list](https://raw.githubusercontent.com/SebastianFalborg/JobsJobsJobs/main/docs/Screenshots/History%20tab.png)

The History tab supports:

- **Filter by job** — dropdown sourced from the current jobs list
- **Filter by status** — multi-select chips for `Succeeded`, `Failed`, `Stopped`, `Running`, `Ignored`, `Idle`
- **Filter by trigger** — `Automatic` or `Manual`
- **Filter by date range** — `From` and `To` (day-level dates in your local time; the `To` date includes the full day up to 23:59:59.999 local). The inputs constrain each other so you cannot pick a `To` before `From`. Day boundaries are your browser's local time and are converted to UTC before reaching the server, so runs are matched against the local wall-clock day. Two corner cases to be aware of: picking the DST fall-back day can miss runs inside the repeated hour, and ops users in different time zones will see different result sets for the same picked date. Use the job / status / trigger / search filters if you need precision inside a DST transition or a tight UTC window.
- **Free-text search** — matches against the stored error, the stored message, and every log line for a run (`LIKE` with automatic wildcards, debounced 300 ms; minimum 3 characters so the log scan never runs on a single-letter input)
- **Paginated list** — 10 / 25 / 50 runs per page, sorted newest first
- **Inline log drill-down** — each row has a `Show logs` toggle that lazily fetches every log line for that run

Each job card in the `Jobs` tab has a `View in history →` link that jumps to the History tab with the alias pre-filtered, so you can drill from a misbehaving job into its full recent history in one click.

Auto-refresh is paused while the History tab is active so your filters and pagination do not reset underneath you; use `Search` on the History tab when you want to re-run the current query.

**Search performance caveat.** Search runs as parameterised `LIKE '%term%'` against the run and log tables. The search is ignored when fewer than 3 characters are typed, so a single-letter pattern can never trigger a full-table scan; the client hints this in a small note under the input. On sites with very large log tables (tens of millions of rows, typically because a spammy job bypassed retention) the search can still take several seconds even with a 3-character minimum. Retention defaults to 100 runs per job and 30 days, which keeps the search fast on typical installs; narrow the job / date filters before searching if you need to.

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

## Run history retention

The package prunes old run history automatically. By default it keeps the **100 most recent runs per job** and runs up to **30 days old**, whichever is stricter. An internal recurring cleanup job runs once per hour, hidden from the dashboard.

> **Behavior change from 1.4.x.** Previous versions retained run history indefinitely. Upgrading to 1.5.0 will start pruning old runs the first time the cleanup job fires. If you upgraded from 1.4.x and have a very large `JobsJobsJobsBackgroundJobRun` / `JobsJobsJobsBackgroundJobRunLog` table, the first sweep may do significant DELETE work; run it outside peak hours or disable retention temporarily and truncate the tables manually instead.

> **Upgrade note for 1.6.0.** Startup runs one new migration (`v6`) that creates a single non-clustered index on `JobsJobsJobsBackgroundJobRun(StartedAt DESC)` so the cross-job History tab query stays fast. On typical installs (retention enabled since 1.5.x) the table is small and the index build is near-instant. If you are jumping from 1.4.x with a multi-million-row run table, the `CREATE INDEX` briefly locks the table on SQL Server Standard Edition and runs against the full row set, so plan the upgrade for a maintenance window or create the index manually ahead of time — the migration checks for the index by name (`IX_JobsJobsJobsBackgroundJobRun_StartedAt`) and no-ops if it is already present.

Configure retention via `BackgroundJobDashboardOptions.RunHistoryRetention` in `appsettings.json` or a composer. All settings are optional and fall back to the defaults below:

```json
{
  "BackgroundJobDashboard": {
    "RunHistoryRetention": {
      "Enabled": true,
      "MaxRunsPerJob": 100,
      "MaxAge": "30.00:00:00",
      "SweepInterval": "01:00:00",
      "DeleteBatchSize": 500
    }
  }
}
```

- Set `Enabled: false` to turn retention off (history will grow unbounded again).
- Set `MaxRunsPerJob: 0` to disable the count-based rule and rely only on `MaxAge`.
- Set `MaxAge: "00:00:00"` to disable the age-based rule and rely only on `MaxRunsPerJob`.
- `SweepInterval` is clamped to a minimum of 1 minute.
- `DeleteBatchSize` controls how many rows are deleted per DB round-trip; lower this if you see lock contention on very large sweeps.

The cleanup job itself writes no entries to the run history tables.

## Non-goals and roadmap

This package is intentionally focused.

- It is not trying to become a general-purpose queue processing platform
- It is not trying to replicate Hangfire features like batches, continuations, or distributed worker orchestration
- It is meant to improve visibility and control for recurring Umbraco background jobs

Planned improvements should continue to focus on documentation, dashboard clarity, and operational insight rather than turning the package into a full workflow engine.
