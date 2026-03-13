# Jobs Jobs Jobs 
 
[![Downloads](https://img.shields.io/nuget/dt/Umbraco.Community.JobsJobsJobs?color=cc9900)](https://www.nuget.org/packages/Umbraco.Community.JobsJobsJobs/)
[![NuGet](https://img.shields.io/nuget/vpre/Umbraco.Community.JobsJobsJobs?color=0273B3)](https://www.nuget.org/packages/Umbraco.Community.JobsJobsJobs)
[![GitHub license](https://img.shields.io/github/license/NotSoap/JobsJobsJobs?color=8AB803)](https://github.com/NotSoap/JobsJobsJobs/blob/main/LICENSE)

Jobs Jobs Jobs adds a backoffice dashboard for recurring Umbraco background jobs, including runtime status and manual triggering.

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

## Use

Open the Umbraco backoffice and go to `Settings -> Background Jobs`.