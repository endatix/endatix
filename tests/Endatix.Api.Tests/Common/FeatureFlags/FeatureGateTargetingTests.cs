using System.Text;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Framework.FeatureFlags;
using Endatix.Infrastructure.FeatureFlags;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FF = Endatix.Framework.FeatureFlags.FeatureFlags;

namespace Endatix.Api.Tests.Common.FeatureFlags;

public sealed class FeatureGateTargetingTests
{
  private const string TenantTargetingConfig = """
        {
          "Endatix": {
            "FeatureFlags": {
              "DataLists": {
                "EnabledFor": [
                  {
                    "Name": "Microsoft.Targeting",
                    "Parameters": {
                      "Audience": {
                        "Groups": [
                          { "Name": "tenant-1", "RolloutPercentage": 100 }
                        ]
                      }
                    }
                  }
                ]
              }
            }
          }
        }
        """;

  [Fact]
  public async Task IsEnabledAsync_returns_true_when_tenant_matches_targeting_group()
  {
    // Arrange
    await using var provider = CreateProvider(tenantId: 1);

    using var scope = provider.CreateScope();
    var gate = scope.ServiceProvider.GetRequiredService<IFeatureGate>();

    // Act
    var enabled = await gate.IsEnabledAsync(FF.DataLists, TestContext.Current.CancellationToken);

    // Assert
    Assert.True(enabled);
  }

  [Fact]
  public async Task IsEnabledAsync_returns_false_when_tenant_does_not_match_targeting_group()
  {
    // Arrange
    await using var provider = CreateProvider(tenantId: 2);

    using var scope = provider.CreateScope();
    var gate = scope.ServiceProvider.GetRequiredService<IFeatureGate>();

    // Act
    var enabled = await gate.IsEnabledAsync(FF.DataLists, TestContext.Current.CancellationToken);

    // Assert
    Assert.False(enabled);
  }

  [Fact]
  public async Task IsEnabledAsync_returns_false_when_targeting_is_not_registered()
  {
    // Arrange
    IConfiguration configuration = new ConfigurationBuilder()
        .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(TenantTargetingConfig)))
        .Build();

    var services = new ServiceCollection();
    services.AddLogging();
    services.AddEndatixFeatureFlags(configuration);
    services.AddScoped<ITenantContext>(_ => new TestTenantContext(1));
    services.AddScoped<IUserContext>(_ => new TestUserContext("user-1"));

    await using var provider = services.BuildServiceProvider();

    using var scope = provider.CreateScope();
    var gate = scope.ServiceProvider.GetRequiredService<IFeatureGate>();

    // Act
    var enabled = await gate.IsEnabledAsync(FF.DataLists, TestContext.Current.CancellationToken);

    // Assert
    Assert.False(enabled);
  }

  private static ServiceProvider CreateProvider(long tenantId)
  {
    IConfiguration configuration = new ConfigurationBuilder()
        .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(TenantTargetingConfig)))
        .Build();

    var services = new ServiceCollection();
    services.AddLogging();
    services.AddEndatixFeatureFlags(configuration)
        .WithEndatixTargeting();
    services.AddScoped<ITenantContext>(_ => new TestTenantContext(tenantId));
    services.AddScoped<IUserContext>(_ => new TestUserContext("user-1"));

    return services.BuildServiceProvider();
  }

  private sealed class TestTenantContext(long tenantId) : ITenantContext
  {
    public long TenantId => tenantId;
  }

  private sealed class TestUserContext(string userId) : IUserContext
  {
    public bool IsAnonymous => false;
    public bool IsAuthenticated => true;
    public string? GetCurrentUserId() => userId;
    public User? GetCurrentUser() => new User(1, userId, "test@example.com", true);
  }
}
