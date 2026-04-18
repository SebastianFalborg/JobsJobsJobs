using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Migrations;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Runtime;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Cms.Infrastructure.Migrations.Upgrade;
using Umbraco.Cms.Infrastructure.Notifications;

namespace JobsJobsJobs.Infrastructure.BackgroundJobs;

internal sealed class BackgroundJobRunMigrationHandler
    : INotificationAsyncHandler<UmbracoApplicationStartingNotification>,
        INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private readonly ICoreScopeProvider _scopeProvider;
    private readonly IMigrationPlanExecutor _migrationPlanExecutor;
    private readonly IKeyValueService _keyValueService;
    private readonly IRuntimeState _runtimeState;
    private readonly ILogger<BackgroundJobRunMigrationHandler> _logger;
    private int _hasRun;

    public BackgroundJobRunMigrationHandler(
        ICoreScopeProvider scopeProvider,
        IMigrationPlanExecutor migrationPlanExecutor,
        IKeyValueService keyValueService,
        IRuntimeState runtimeState,
        ILogger<BackgroundJobRunMigrationHandler> logger
    )
    {
        _scopeProvider = scopeProvider;
        _migrationPlanExecutor = migrationPlanExecutor;
        _keyValueService = keyValueService;
        _runtimeState = runtimeState;
        _logger = logger;
    }

    public Task HandleAsync(UmbracoApplicationStartingNotification notification, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "BackgroundJobRunMigrationHandler triggered by UmbracoApplicationStartingNotification (RuntimeLevel: {Level}).",
            _runtimeState.Level
        );
        return EnsureMigrationsAsync();
    }

    public Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "BackgroundJobRunMigrationHandler triggered by UmbracoApplicationStartedNotification (RuntimeLevel: {Level}).",
            _runtimeState.Level
        );
        return EnsureMigrationsAsync();
    }

    private Task EnsureMigrationsAsync()
    {
        if (_runtimeState.Level < RuntimeLevel.Run)
        {
            _logger.LogDebug("BackgroundJobRunMigrationHandler skipping: RuntimeLevel is {Level}, requires Run.", _runtimeState.Level);
            return Task.CompletedTask;
        }

        if (Interlocked.Exchange(ref _hasRun, 1) == 1)
        {
            return Task.CompletedTask;
        }

        return RunMigrationsAsync();
    }

    private async Task RunMigrationsAsync()
    {
        _logger.LogInformation("Running JobsJobsJobs background job run history migration.");
        try
        {
            var migrationPlan = new MigrationPlan("JobsJobsJobs.BackgroundJobRunHistory.v4")
                .From(string.Empty)
                .To<CreateBackgroundJobRunTablesMigration>("background-job-run-tables-v4");

            var upgrader = new Upgrader(migrationPlan);
            await upgrader.ExecuteAsync(_migrationPlanExecutor, _scopeProvider, _keyValueService);
            _logger.LogInformation("JobsJobsJobs background job run history migration completed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "JobsJobsJobs background job run history migration failed. Run history will not be persisted until the migration succeeds."
            );
        }
    }
}

internal abstract class BackgroundJobRunTablesMigrationBase : AsyncMigrationBase
{
    protected BackgroundJobRunTablesMigrationBase(IMigrationContext context)
        : base(context) { }

    protected void CreateRunTable()
    {
        Create
            .Table(BackgroundJobRunDto.TableName)
            .WithColumn(nameof(BackgroundJobRunDto.Id))
            .AsGuid()
            .NotNullable()
            .PrimaryKey("PK_JobsJobsJobsBackgroundJobRun")
            .WithColumn(nameof(BackgroundJobRunDto.JobAlias))
            .AsString(255)
            .NotNullable()
            .WithColumn(nameof(BackgroundJobRunDto.JobName))
            .AsString(255)
            .NotNullable()
            .WithColumn(nameof(BackgroundJobRunDto.Trigger))
            .AsString(64)
            .NotNullable()
            .WithColumn(nameof(BackgroundJobRunDto.Status))
            .AsString(64)
            .NotNullable()
            .WithColumn(nameof(BackgroundJobRunDto.StartedAt))
            .AsDateTime()
            .NotNullable()
            .WithColumn(nameof(BackgroundJobRunDto.CompletedAt))
            .AsDateTime()
            .Nullable()
            .WithColumn(nameof(BackgroundJobRunDto.DurationMs))
            .AsInt64()
            .Nullable()
            .WithColumn(nameof(BackgroundJobRunDto.Message))
            .AsString(4000)
            .Nullable()
            .WithColumn(nameof(BackgroundJobRunDto.Error))
            .AsString(4000)
            .Nullable()
            .Do();
    }

    protected void CreateRunLogTable()
    {
        Create
            .Table(BackgroundJobRunLogDto.TableName)
            .WithColumn(nameof(BackgroundJobRunLogDto.Id))
            .AsGuid()
            .NotNullable()
            .PrimaryKey("PK_JobsJobsJobsBackgroundJobRunLog")
            .WithColumn(nameof(BackgroundJobRunLogDto.RunId))
            .AsGuid()
            .NotNullable()
            .WithColumn(nameof(BackgroundJobRunLogDto.Level))
            .AsString(64)
            .NotNullable()
            .WithColumn(nameof(BackgroundJobRunLogDto.Message))
            .AsString(4000)
            .NotNullable()
            .WithColumn(nameof(BackgroundJobRunLogDto.LoggedAt))
            .AsDateTime()
            .NotNullable()
            .Do();
    }
}

internal sealed class CreateBackgroundJobRunTablesMigration : BackgroundJobRunTablesMigrationBase
{
    public CreateBackgroundJobRunTablesMigration(IMigrationContext context)
        : base(context) { }

    protected override Task MigrateAsync()
    {
        if (TableExists(BackgroundJobRunDto.TableName) is false)
        {
            CreateRunTable();
        }

        if (TableExists(BackgroundJobRunLogDto.TableName) is false)
        {
            CreateRunLogTable();
        }

        return Task.CompletedTask;
    }
}
