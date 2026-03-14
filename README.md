# Jobs Jobs Jobs

Jobs Jobs Jobs adds a backoffice dashboard for recurring Umbraco background jobs, including runtime status and manual triggering.

## Package usage

If you want the install and usage guide for the package itself, see:

```text
docs/README_nuget.md
```

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

## Repository structure

```text
src/JobsJobsJobs                Package project
src/JobsJobsJobs.TestSite       Local test site
docs/README_nuget.md            Package install and usage guide
```
