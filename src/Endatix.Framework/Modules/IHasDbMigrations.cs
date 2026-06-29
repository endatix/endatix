namespace Endatix.Framework.Modules;

/// <summary>
/// Marks a module that ships EF Core migrations. Used for startup diagnostics when
/// <see cref="EndatixModuleBuilder.MigrationContributorRegistered"/> is false after
/// <see cref="IEndatixModule.ConfigureServices"/>.
/// </summary>
public interface IHasDbMigrations
{
}
