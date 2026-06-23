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
