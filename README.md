# Jobs Jobs Jobs

Jobs Jobs Jobs is a simple job scheduler and backoffice dashboard for Umbraco, built on top of Umbraco's existing background jobs infrastructure. It adds runtime status, manual triggering, persisted run history, cooperative stop support, and opt-in CRON scheduling.

## Why use this over Hangfire?

Jobs Jobs Jobs is for Umbraco projects that want a simpler, more Umbraco-native alternative to external schedulers when the goal is scheduled jobs with visibility and control inside the Umbraco backoffice.

- Lightweight setup with no separate job server or external dashboard product to introduce
- Built on top of Umbraco's own background jobs model instead of adding a separate scheduling platform
- Focused backoffice UI for scheduled Umbraco jobs, status, manual runs, and persisted run history
- Lower operational overhead for simple scheduled tasks where a full background processing platform would be overkill
- Better fit when the goal is visibility and control inside Umbraco rather than queue orchestration

## Package usage

If you want the install and usage guide for the package itself, see:

```text
docs/README_nuget.md
```

The package guide includes:

- a choose-your-job-type guide for `RecurringBackgroundJobBase` and `CronBackgroundJobBase`
- stop-support setup
- recurring and CRON job registration
- consistent builder-based registration for Umbraco composers
- extending a chosen job type with stop support through capability interfaces
- how `CRON` relates to `Period` and `Delay`
- multi-window CRON schedules
- best practices for cooperative cancellation

## Build

Run the client build from the repository root:

```powershell
npm run build
```

Or run it directly from the client project:

```powershell
npm run build
```

in:

```text
src/JobsJobsJobs/Client
```

## Pack

Create the NuGet package from:

```text
src/JobsJobsJobs
```

with:

```powershell
dotnet pack -c Release
```

`dotnet pack` automatically runs the client build before packaging.

## Non-goals

This package does not try to replace every advanced background processing scenario.

- It is not a full queue processing platform
- It is not trying to provide Hangfire-style batches, continuations, or distributed worker orchestration
- It is focused on recurring Umbraco background jobs, visibility, and operational clarity

## Repository structure

```text
src/JobsJobsJobs                Package project
src/JobsJobsJobs.TestSite       Local test site
docs/README_nuget.md            Package install and usage guide
```
