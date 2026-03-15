# Jobs Jobs Jobs

Jobs Jobs Jobs adds a backoffice dashboard for recurring Umbraco background jobs, including runtime status and manual triggering.

It also supports cooperative stop requests for jobs that opt in with `IStoppableRecurringBackgroundJob` and observe the provided cancellation signal.

## Why use this over Hangfire?

Jobs Jobs Jobs is aimed at teams that are already using Umbraco recurring background jobs and want a simpler, more Umbraco-native experience.

- Lightweight setup with no separate job server or extra dashboard product to introduce
- Focused backoffice UI for recurring Umbraco jobs, status, manual runs, and persisted run history
- Lower operational overhead for simple scheduled tasks where a full background processing platform would be overkill
- Better fit when the goal is visibility and control of Umbraco jobs rather than queue orchestration

## Package usage

If you want the install and usage guide for the package itself, see:

```text
docs/README_nuget.md
```

The package guide includes the stop-support setup, how to inject `IBackgroundJobExecutionCancellation`, and best practices for cooperative cancellation.

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
