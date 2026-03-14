using System.Threading;
using System.Threading.Tasks;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Migrations;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Runtime;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Cms.Infrastructure.Migrations.Upgrade;

namespace JobsJobsJobs.BackgroundJobs;

internal sealed class BackgroundJobRunMigrationHandler : INotificationAsyncHandler<UmbracoApplicationStartingNotification>
{
    private readonly ICoreScopeProvider _scopeProvider;
    private readonly IMigrationPlanExecutor _migrationPlanExecutor;
    private readonly IKeyValueService _keyValueService;
    private readonly IRuntimeState _runtimeState;

    public BackgroundJobRunMigrationHandler(
        ICoreScopeProvider scopeProvider,
        IMigrationPlanExecutor migrationPlanExecutor,
        IKeyValueService keyValueService,
        IRuntimeState runtimeState)
    {
        _scopeProvider = scopeProvider;
        _migrationPlanExecutor = migrationPlanExecutor;
        _keyValueService = keyValueService;
        _runtimeState = runtimeState;
    }

    public Task HandleAsync(UmbracoApplicationStartingNotification notification, CancellationToken cancellationToken)
    {
        if (_runtimeState.Level < RuntimeLevel.Run)
        {
            return Task.CompletedTask;
        }

        var migrationPlan = new MigrationPlan("JobsJobsJobs.BackgroundJobRunHistory.v2")
            .From(string.Empty)
            .To<RecreateBackgroundJobRunTablesMigration>("background-job-run-tables-v2");

        var upgrader = new Upgrader(migrationPlan);
        upgrader.Execute(_migrationPlanExecutor, _scopeProvider, _keyValueService);

        return Task.CompletedTask;
    }
}

internal abstract class BackgroundJobRunTablesMigrationBase : MigrationBase
{
    protected BackgroundJobRunTablesMigrationBase(IMigrationContext context)
        : base(context)
    {
    }

    protected void CreateRunTable()
        => Create.Table(BackgroundJobRunDto.TableName)
            .WithColumn(nameof(BackgroundJobRunDto.Id)).AsGuid().PrimaryKey().NotNullable()
            .WithColumn(nameof(BackgroundJobRunDto.JobAlias)).AsString(255).NotNullable()
            .WithColumn(nameof(BackgroundJobRunDto.JobName)).AsString(255).NotNullable()
            .WithColumn(nameof(BackgroundJobRunDto.Trigger)).AsString(64).NotNullable()
            .WithColumn(nameof(BackgroundJobRunDto.Status)).AsString(64).NotNullable()
            .WithColumn(nameof(BackgroundJobRunDto.StartedAt)).AsDateTime().NotNullable()
            .WithColumn(nameof(BackgroundJobRunDto.CompletedAt)).AsDateTime().Nullable()
            .WithColumn(nameof(BackgroundJobRunDto.DurationMs)).AsInt64().Nullable()
            .WithColumn(nameof(BackgroundJobRunDto.Message)).AsString(4000).Nullable()
            .WithColumn(nameof(BackgroundJobRunDto.Error)).AsString(4000).Nullable()
            .Do();

    protected void CreateRunLogTable()
        => Create.Table(BackgroundJobRunLogDto.TableName)
            .WithColumn(nameof(BackgroundJobRunLogDto.Id)).AsGuid().PrimaryKey().NotNullable()
            .WithColumn(nameof(BackgroundJobRunLogDto.RunId)).AsGuid().NotNullable()
            .WithColumn(nameof(BackgroundJobRunLogDto.Level)).AsString(64).NotNullable()
            .WithColumn(nameof(BackgroundJobRunLogDto.Message)).AsString(4000).NotNullable()
            .WithColumn(nameof(BackgroundJobRunLogDto.LoggedAt)).AsDateTime().NotNullable()
            .Do();
}

internal sealed class CreateBackgroundJobRunTablesMigration : BackgroundJobRunTablesMigrationBase
{
    public CreateBackgroundJobRunTablesMigration(IMigrationContext context)
        : base(context)
    {
    }

    protected override void Migrate()
    {
        if (TableExists(BackgroundJobRunDto.TableName) is false)
        {
            CreateRunTable();
        }

        if (TableExists(BackgroundJobRunLogDto.TableName) is false)
        {
            CreateRunLogTable();
        }
    }
}

internal sealed class RecreateBackgroundJobRunTablesMigration : BackgroundJobRunTablesMigrationBase
{
    public RecreateBackgroundJobRunTablesMigration(IMigrationContext context)
        : base(context)
    {
    }

    protected override void Migrate()
    {
        if (TableExists(BackgroundJobRunLogDto.TableName))
        {
            Delete.Table(BackgroundJobRunLogDto.TableName).Do();
        }

        if (TableExists(BackgroundJobRunDto.TableName))
        {
            Delete.Table(BackgroundJobRunDto.TableName).Do();
        }

        CreateRunTable();
        CreateRunLogTable();
    }
}
