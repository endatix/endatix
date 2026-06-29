using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Framework.Modules;

/// <summary>
/// Builder passed to <see cref="IEndatixModule.ConfigureServices"/> during host finalization.
/// </summary>
public sealed class EndatixModuleBuilder(IServiceCollection services, IConfiguration configuration)
{
  public IServiceCollection Services { get; } = services;

  public IConfiguration Configuration { get; } = configuration;

  /// <summary>
  /// Whether a migration contributor was registered for this module (via <c>AddDbContextWithMigrations</c>).
  /// </summary>
  public bool MigrationContributorRegistered { get; private set; }

  /// <summary>
  /// Marks that a startup migration contributor was registered for this module.
  /// Called by Infrastructure persistence extensions — not intended for module authors.
  /// </summary>
  public void MarkMigrationContributorRegistered() => MigrationContributorRegistered = true;

  /// <summary>
  /// Binds options from configuration with data-annotation validation on start.
  /// </summary>
  public EndatixModuleBuilder AddOptions<TOptions>(string sectionName)
      where TOptions : class
  {
    Services.AddOptions<TOptions>()
        .BindConfiguration(sectionName)
        .ValidateDataAnnotations()
        .ValidateOnStart();

    return this;
  }
}
