using Endatix.Api.Infrastructure.Cors;
using Endatix.Framework.Hosting;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute.ReceivedExtensions;

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
        _wildcardSearcher = new CorsWildcardSearcher();
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
            AllowedOrigins = ["*"]
        };
        CorsPolicySetting[] corsPolicies = [policy];
        var corsSettings = Options.Create(new CorsSettings()
        {
            CorsPolicies = corsPolicies
        });
        var configurator = CreateCorsConfigurator(corsSettings);

        // Act
        configurator.Configure(_options);

        // Assert
        var actualPolicy = _options.GetPolicy(ruleNameUnderTest);

        actualPolicy.Should().NotBeNull();
        actualPolicy?.AllowAnyOrigin.Should().BeTrue();
    }

    [Fact]
    public void Configure_OriginsIgnoreAllWildcard_SetsNoOrigins()
    {
        // Arrange;
        var ruleNameUnderTest = "SomeRule";
        var corsSettings = Options.Create(new CorsSettings()
        {
            CorsPolicies = [
                new CorsPolicySetting() {
                    PolicyName = ruleNameUnderTest,
                    AllowedOrigins = [" -"]
            }]
        });
        var configurator = CreateCorsConfigurator(corsSettings);

        // Act
        configurator.Configure(_options);

        // Assert
        var actualPolicy = _options.GetPolicy(ruleNameUnderTest);

        actualPolicy.Should().NotBeNull();
        actualPolicy?.Origins.Should().BeNullOrEmpty();
    }

    [Fact]
    public void Configure_OriginsProvided_SetsOriginsToCorsPolicy()
    {
        // Arrange;
        var ruleNameUnderTest = "SomeRule";
        IList<string> providedOrigins = ["origin1", "origin2", "origin3"];
        var corsSettings = Options.Create(new CorsSettings()
        {
            CorsPolicies = [
                new CorsPolicySetting() {
                    PolicyName = ruleNameUnderTest,
                    AllowedOrigins = providedOrigins
            }]
        });
        var configurator = CreateCorsConfigurator(corsSettings);

        // Act
        configurator.Configure(_options);

        // Assert
        var actualPolicy = _options.GetPolicy(ruleNameUnderTest);

        actualPolicy.Should().NotBeNull();
        actualPolicy?.Origins.Should().Equal(providedOrigins);
    }

    [Fact]
    public void Configure_HeadersIgnoreAllWildcard_SetsNoHeaders()
    {
        // Arrange;
        var ruleNameUnderTest = "SomeRule";
        var corsSettings = Options.Create(new CorsSettings()
        {
            CorsPolicies = [
                new CorsPolicySetting() {
                    PolicyName = ruleNameUnderTest,
                    AllowedHeaders = ["-"]
            }]
        });
        var configurator = CreateCorsConfigurator(corsSettings);

        // Act
        configurator.Configure(_options);

        // Assert
        var actualPolicy = _options.GetPolicy(ruleNameUnderTest);

        actualPolicy.Should().NotBeNull();
        actualPolicy?.Headers.Should().BeNullOrEmpty();
    }

    [Fact]
    public void Configure_MethodsAllowAllWildcard_SetsAnyMethod()
    {
        // Arrange;
        var ruleNameUnderTest = "SomeRule";
        var corsSettings = Options.Create(new CorsSettings()
        {
            CorsPolicies = [
                new CorsPolicySetting()
                        {
                            PolicyName = ruleNameUnderTest,
                            AllowedMethods = ["*"]
                        }]
        });
        var configurator = CreateCorsConfigurator(corsSettings);

        // Act
        configurator.Configure(_options);

        // Assert
        var actualPolicy = _options.GetPolicy(ruleNameUnderTest);

        actualPolicy?.AllowAnyMethod.Should().BeTrue();
    }

    [Fact]
    public void Configure_MethodsProvided_SetsAllowedMethods()
    {
        // Arrange;
        var ruleNameUnderTest = "SomeRule";
        var corsSettings = Options.Create(new CorsSettings()
        {
            CorsPolicies = [
                new CorsPolicySetting() {
                    PolicyName = ruleNameUnderTest,
                    AllowedMethods = ["GET", "POST"]
            }]
        });
        var configurator = CreateCorsConfigurator(corsSettings);

        // Act
        configurator.Configure(_options);

        // Assert
        var actualPolicy = _options.GetPolicy(ruleNameUnderTest);

        actualPolicy?.Methods.Should().Equal(["GET", "POST"]);
    }

    [Fact]
    public void Configure_MethodsIgnoreAllWildcard_SetsNoMethods()
    {
        // Arrange;
        var ruleNameUnderTest = "SomeRule";
        var corsSettings = Options.Create(new CorsSettings()
        {
            CorsPolicies = [
                new CorsPolicySetting() {
                    PolicyName = ruleNameUnderTest,
                    AllowedMethods = ["-"]
            }]
        });
        var configurator = CreateCorsConfigurator(corsSettings);

        // Act
        configurator.Configure(_options);

        // Assert
        var actualPolicy = _options.GetPolicy(ruleNameUnderTest);

        actualPolicy.Should().NotBeNull();
        actualPolicy?.Methods.Should().BeNullOrEmpty();
    }


    [Fact]
    public void Configure_ExposedHeadersProvided_SetsExposedHeaders()
    {
        // Arrange;
        var ruleNameUnderTest = "SomeRule";
        IList<string> providedExposedHeaders = ["x-header-1", "x-header-2"];
        var corsSettings = Options.Create(new CorsSettings()
        {
            CorsPolicies = [
                new CorsPolicySetting() {
                    PolicyName = ruleNameUnderTest,
                    ExposedHeaders = providedExposedHeaders
            }]
        });
        var configurator = CreateCorsConfigurator(corsSettings);

        // Act
        configurator.Configure(_options);

        // Assert
        var actualPolicy = _options.GetPolicy(ruleNameUnderTest);

        actualPolicy.Should().NotBeNull();
        actualPolicy?.ExposedHeaders.Should().Equal(providedExposedHeaders);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Configure_AllowCredentialsProvided_SetsSupportsCredentials(bool allowCredentialsValue, bool expectedValue)
    {
        // Arrange;
        var ruleNameUnderTest = "SomeRule";
        var corsSettings = Options.Create(new CorsSettings()
        {
            CorsPolicies = [
                new CorsPolicySetting() {
                    PolicyName = ruleNameUnderTest,
                    AllowCredentials = allowCredentialsValue
            }]
        });
        var configurator = CreateCorsConfigurator(corsSettings);

        // Act
        configurator.Configure(_options);

        // Assert
        var actualPolicy = _options.GetPolicy(ruleNameUnderTest);

        actualPolicy.Should().NotBeNull();
        actualPolicy?.SupportsCredentials.Should().Be(expectedValue);
    }

    [Fact]
    public void Configure_AllowCredentialsAndAnyOriginProvided_FiresWarning()
    {
        // Arrange;
        var ruleNameUnderTest = "SomeRule";
        var corsSettings = Options.Create(new CorsSettings()
        {
            CorsPolicies = [
                new CorsPolicySetting() {
                    PolicyName = ruleNameUnderTest,
                    AllowCredentials = true,
                    AllowedOrigins = ["*"]
            }]
        });
        var configurator = CreateCorsConfigurator(corsSettings);

        // Act
        configurator.Configure(_options);

        // Assert
        var expectedFormattedMessage = "Ignoring AllowCredentials and disallowing credentials. Details: The CORS protocol does not allow specifying a wildcard (any) origin and credentials at the same time. Configure the CORS policy by listing individual origins if credentials needs to be supported.";
        _logger.Received(1).Log(
          Arg.Is(LogLevel.Warning),
          Arg.Is<EventId>(0),
          Arg.Is<object>(x => x.ToString() == expectedFormattedMessage),
          Arg.Is<Exception>(x => x == null),
          Arg.Any<Func<object, Exception?, string>>());

        var actualPolicy = _options.GetPolicy(ruleNameUnderTest);
        actualPolicy.Should().NotBeNull();
        actualPolicy?.AllowAnyOrigin.Should().BeTrue();
        actualPolicy?.SupportsCredentials.Should().BeFalse();
    }

    [Fact]
    public void Configure_WhenDefaultPolicyNameExists_SetsDefaultPolicy()
    {
        // Arrange
        var corsSettings = Options.Create(new CorsSettings
        {
            DefaultPolicyName = "DefaultPolicy",
            CorsPolicies = new List<CorsPolicySetting>
            {
                new CorsPolicySetting { PolicyName = "DefaultPolicy" },
                new CorsPolicySetting { PolicyName = "AnotherPolicy" }
            }
        });

        var configurator = CreateCorsConfigurator(corsSettings);

        // Act
        configurator.Configure(_options);

        // Assert
        _options.DefaultPolicyName.Should().Be("DefaultPolicy");
    }

    [Fact]
    public void Configure_WhenWrongDefaultPolicyName_SetsFirstPolicyAsDefault()
    {
        // Arrange
        var corsSettings = Options.Create(new CorsSettings
        {
            DefaultPolicyName = "WrongName",
            CorsPolicies = [new CorsPolicySetting { PolicyName = "FirstPolicyName" }]
        });

        var configurator = CreateCorsConfigurator(corsSettings);

        // Act
        configurator.Configure(_options);

        // Assert
        _options.DefaultPolicyName.Should().Be("FirstPolicyName");
        _options.GetPolicy("FirstPolicyName").Should().NotBeNull();
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
