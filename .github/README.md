# Jobs Jobs Jobs 

[![Downloads](https://img.shields.io/nuget/dt/Umbraco.Community.JobsJobsJobs?color=cc9900)](https://www.nuget.org/packages/Umbraco.Community.JobsJobsJobs/)
[![NuGet](https://img.shields.io/nuget/vpre/Umbraco.Community.JobsJobsJobs?color=0273B3)](https://www.nuget.org/packages/Umbraco.Community.JobsJobsJobs)
[![GitHub license](https://img.shields.io/github/license/SebastianFalborg/JobsJobsJobs?color=8AB803)](../LICENSE)

Jobs Jobs Jobs adds a backoffice dashboard for recurring Umbraco background jobs, including runtime status, manual triggering, persisted run history, and stored run logs.

## Why use this instead of Hangfire?

This package is aimed at Umbraco solutions that already use `IRecurringBackgroundJob` and want a simpler, more Umbraco-native dashboard experience.

- Focused UI for recurring Umbraco jobs
- Manual triggering directly from the backoffice
- Persisted run history and stored logs
- Lower setup and operational overhead for simple scheduled work

## Installation

Add the package to an existing Umbraco website (v17+) from NuGet:

`dotnet add package Umbraco.Community.JobsJobsJobs`

For the full install and usage guide, see:

```text
docs/README_nuget.md
```

After installing the package, register one or more `IRecurringBackgroundJob` implementations and open `Settings -> Background Jobs` in the Umbraco backoffice.

## Contributing

Contributions to this package are most welcome! Please read the [Contributing Guidelines](CONTRIBUTING.md).

## Non-goals

- This package is not trying to be a full queue processing platform
- It does not aim to replicate Hangfire batches, continuations, or distributed worker orchestration
- It is focused on operational visibility and control for recurring Umbraco background jobs