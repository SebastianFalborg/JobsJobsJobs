<#
.SYNOPSIS
  Packs the JobsJobsJobs nupkg as a reproducible prerelease for local test sites.

.DESCRIPTION
  Builds the client bundle and the .NET project from a clean slate so the
  static web assets (umbraco-package.json + JS bundles) always end up inside
  the final nupkg. Skipping the clean step is what caused the 1.6.0-alpha002
  nupkg to ship with only DLLs, which broke the backoffice dashboard on
  consumer sites.

.PARAMETER Version
  The full prerelease version to use (e.g. "1.6.0-alpha004"). Required so
  the umbraco-package.json stays in lockstep with the csproj and so every
  iteration gets a unique cache folder in the consumer's ~/.nuget packages.

.EXAMPLE
  .\scripts\pack-alpha.ps1 -Version 1.6.0-alpha004
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [ValidatePattern('^\d+\.\d+\.\d+(-[A-Za-z0-9.-]+)?$')]
  [string] $Version
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$csprojPath = Join-Path $repoRoot 'src/JobsJobsJobs/JobsJobsJobs.csproj'
$umbracoPackageJsonPath = Join-Path $repoRoot 'src/JobsJobsJobs/Client/public/umbraco-package.json'
$artifactsPath = Join-Path $repoRoot 'artifacts'

if (-not (Test-Path $csprojPath)) { throw "Cannot find $csprojPath" }
if (-not (Test-Path $umbracoPackageJsonPath)) { throw "Cannot find $umbracoPackageJsonPath" }

Write-Host "==> Setting version to $Version" -ForegroundColor Cyan
(Get-Content $csprojPath -Raw) `
  -replace '<Version>[^<]+</Version>', "<Version>$Version</Version>" `
  | Set-Content $csprojPath -Encoding UTF8 -NoNewline

$umbracoPackage = Get-Content $umbracoPackageJsonPath -Raw | ConvertFrom-Json
$umbracoPackage.version = $Version
$umbracoPackage | ConvertTo-Json -Depth 32 | Set-Content $umbracoPackageJsonPath -Encoding UTF8

Write-Host "==> Cleaning previous build output" -ForegroundColor Cyan
Remove-Item $artifactsPath -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $repoRoot 'src/JobsJobsJobs/wwwroot') -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $repoRoot 'src/JobsJobsJobs/obj') -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $repoRoot 'src/JobsJobsJobs/bin') -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "==> Building client bundle" -ForegroundColor Cyan
npm run build --prefix (Join-Path $repoRoot 'src/JobsJobsJobs/Client')
if ($LASTEXITCODE -ne 0) { throw "Client build failed" }

Write-Host "==> Building .NET project" -ForegroundColor Cyan
dotnet build $csprojPath -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw ".NET build failed" }

Write-Host "==> Packing nupkg" -ForegroundColor Cyan
dotnet pack $csprojPath -c Release -o $artifactsPath --no-build --nologo
if ($LASTEXITCODE -ne 0) { throw "Pack failed" }

$nupkgPath = Join-Path $artifactsPath "Umbraco.Community.JobsJobsJobs.$Version.nupkg"
if (-not (Test-Path $nupkgPath)) { throw "Expected nupkg not produced: $nupkgPath" }

Write-Host "==> Verifying nupkg contents" -ForegroundColor Cyan
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($nupkgPath)
$entryNames = $zip.Entries | ForEach-Object { $_.FullName }
$zip.Dispose()

$requiredEntries = @(
  'staticwebassets/App_Plugins/JobsJobsJobs/umbraco-package.json',
  'staticwebassets/App_Plugins/JobsJobsJobs/jobs-jobs-jobs.js',
  'lib/net10.0/JobsJobsJobs.dll',
  'lib/net10.0/JobsJobsJobs.Core.dll',
  'lib/net10.0/JobsJobsJobs.Infrastructure.dll'
)
$missing = $requiredEntries | Where-Object { $entryNames -notcontains $_ }
if ($missing) {
  throw "nupkg is missing required entries:`n$($missing -join "`n")"
}

Write-Host ""
Write-Host "Packed $nupkgPath" -ForegroundColor Green
Write-Host ""
Write-Host "Install on a test site (from the consumer project folder):" -ForegroundColor Yellow
Write-Host "  dotnet add package Umbraco.Community.JobsJobsJobs --version $Version"
Write-Host ""
