using Endatix.Api.Infrastructure.Cors;
using Endatix.Framework.Hosting;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Endatix.Api.Tests.Infrastructure.Cors;

public class EndpointsCorsConfiguratorTests
{
    private readonly IOptions<CorsSettings> _corsSettings;
    private readonly IAppEnvironment _appEnvironment;
    private readonly ILogger<EndpointsCorsConfigurator> _logger;
    private readonly IWildcardSearcher _wildcardSearcher;
    private readonly CorsOptions _options;
    public EndpointsCorsConfiguratorTests()
    {
        _appEnvironment = Substitute.For<IAppEnvironment>();
        _logger = Substitute.For<ILogger<EndpointsCorsConfigurator>>();
        _corsSettings = Options.Create(new CorsSettings());
        _options = new CorsOptions();
        _wildcardSearcher = Substitute.For<IWildcardSearcher>();
    }

    [Fact]
    public void Configure_Always_AddsDefaultPolicies()
    {
        // Arrange
        var configurator = CreateCorsConfigurator();

        // Act
        configurator.Configure(_options);

        // Assert
        var allowAllPolicy = _options.GetPolicy(EndpointsCorsConfigurator.ALLOW_ALL_POLICY_NAME);
        var disallowAllPolicy = _options.GetPolicy(EndpointsCorsConfigurator.DISALLOW_ALL_POLICY_NAME);

        allowAllPolicy.Should().NotBeNull();
        disallowAllPolicy.Should().NotBeNull();
    }

    [Fact]
    public void Configure_NoPoliciesInDevelopment_SetsCorrectFallback()
    {
        // Arrange
        var emptyCorsSettings = Options.Create(new CorsSettings());
        _ = _appEnvironment.IsDevelopment().Returns(true);
        var configurator = CreateCorsConfigurator(emptyCorsSettings);

        // Act
        configurator.Configure(_options);

        // Assert
        var defaultPolicyName = _options.DefaultPolicyName;

        defaultPolicyName.Should().Be(EndpointsCorsConfigurator.ALLOW_ALL_POLICY_NAME);
        _options.GetPolicy(EndpointsCorsConfigurator.ALLOW_ALL_POLICY_NAME).Should().NotBeNull();
    }

    [Fact]
    public void Configure_NoPoliciesInProduction_SetsCorrectFallback()
    {
        // Arrange
        var emptyCorsSettings = Options.Create(new CorsSettings());
        _ = _appEnvironment.IsDevelopment().Returns(false);
        var configurator = CreateCorsConfigurator(emptyCorsSettings);

        // Act
        configurator.Configure(_options);

        // Assert
        var defaultPolicyName = _options.DefaultPolicyName;

        defaultPolicyName.Should().Be(EndpointsCorsConfigurator.DISALLOW_ALL_POLICY_NAME);
        _options.GetPolicy(EndpointsCorsConfigurator.DISALLOW_ALL_POLICY_NAME).Should().NotBeNull();
    }

    [Fact]
    public void Configure_ListOfPoliciesWithTheSameName_DoesNotThrow()
    {
        // Arrange
        var duplicatedName = "SameName";
        var specificOrigin = "https://hello.world";
        CorsPolicySetting[] corsPolicies = [
            new CorsPolicySetting(){
                PolicyName = duplicatedName
            },
            new CorsPolicySetting(){
                PolicyName = duplicatedName,
                AllowedOrigins = [specificOrigin]
            }
        ];
        var corsSettings = Options.Create(new CorsSettings()
        {
            CorsPolicies = corsPolicies
        });
        var configurator = CreateCorsConfigurator(corsSettings);

        // Act
        configurator.Configure(_options);

        // Assert
        var defaultPolicyName = _options.DefaultPolicyName;
        var policyWithDuplicatedName = _options.GetPolicy(duplicatedName);

        defaultPolicyName.Should().Be(duplicatedName);
        policyWithDuplicatedName.Should().NotBeNull();
        policyWithDuplicatedName?.Origins.Should().Equal([specificOrigin]);
    }

    [Fact]
    public void Configure_OriginsAllowAllWildcard_SetsAnyOrigin()
    {
        // Arrange;
        var ruleNameUnderTest = "SomeRule";
        var policy = new CorsPolicySetting()
        {
            PolicyName = ruleNameUnderTest,
            AllowedOrigins = ["https://some.origin", "asterisk.with.whitespace.should.be.correct", " *", "everything.after.it.should.be.ignored", "-"]
        };
        CorsPolicySetting[] corsPolicies = [policy];
        var corsSettings = Options.Create(new CorsSettings()
        {
            CorsPolicies = corsPolicies
        });
        _wildcardSearcher.SearchForWildcard(policy.AllowedOrigins).Returns(CorsWildcardResult.MatchAll);
        var configurator = CreateCorsConfigurator(corsSettings);

        // Act
        configurator.Configure(_options);

        // Assert
        var actualPolicy = _options.GetPolicy(ruleNameUnderTest);

        actualPolicy.Should().NotBeNull();
        actualPolicy?.AllowAnyOrigin.Should().BeTrue();
    }

    /// <summary>
    /// Creates a custom <see cref="EndpointsCorsConfigurator"/> instance based of provided stubs or returns a default one. Helps with writing shorted and more expressive tests
    /// </summary>
    /// <param name="corsSettingsStub">A optional <see cref="CorsSettings"/> IOptions stub</param>
    /// <param name="loggerStub">A optional <see cref="ILogger"/> stub</param>
    /// <param name="appEnvironmentStub">A optional <see cref="IAppEnvironment"/> stub</param>
    /// <param name="wildcardSearcherStub">A optional <see cref="IWildcardSearcher"/> stub</param>
    /// <returns>The arranged instances to perform the tests upon</returns>
    private EndpointsCorsConfigurator CreateCorsConfigurator(
        IOptions<CorsSettings>? corsSettingsStub = null,
        ILogger<EndpointsCorsConfigurator> loggerStub = null,
        IAppEnvironment appEnvironmentStub = null,
        IWildcardSearcher wildcardSearcherStub = null)
    {
        var corsSettings = corsSettingsStub ?? _corsSettings;
        var logger = loggerStub ?? _logger;
        var appEnvironment = appEnvironmentStub ?? _appEnvironment;
        var wildcardSearcher = wildcardSearcherStub ?? _wildcardSearcher;

        return new EndpointsCorsConfigurator(
            corsSettings,
            logger,
             appEnvironment,
             wildcardSearcher);
    }
}
