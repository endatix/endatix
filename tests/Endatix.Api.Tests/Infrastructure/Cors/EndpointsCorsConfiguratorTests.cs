using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Endatix.Api.Infrastructure.Cors;
using Endatix.Framework.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Endatix.Api.Tests.Infrastructure.Cors;

public class EndpointsCorsConfiguratorTests
{
    private readonly IOptions<CorsSettings> _corsSettings;
    private readonly IEndatixApp _endatixApp;
    private readonly ILogger<EndpointsCorsConfigurator> _logger;

    private readonly IWildcardSearcher _wildcardSearcher;

    private readonly CorsOptions _options;
    public EndpointsCorsConfiguratorTests()
    {
        _endatixApp = Substitute.For<IEndatixApp>();
        _logger = Substitute.For<ILogger<EndpointsCorsConfigurator>>();
        _corsSettings = Options.Create(new CorsSettings());
        _options = new CorsOptions();
        _wildcardSearcher = Substitute.For<IWildcardSearcher>();
    }

    [Fact]
    public void Configure_Always_AddsDefaultPolicies()
    {
        // Arrange
        var configurator = new EndpointsCorsConfigurator(_corsSettings, _logger, _endatixApp, _wildcardSearcher);

        // Act
        configurator.Configure(_options);

        // Assert
        var allowAllPolicy = _options.GetPolicy(EndpointsCorsConfigurator.ALLOW_ALL_POLICY_NAME);
        var disallowAllPolicy = _options.GetPolicy(EndpointsCorsConfigurator.DISALLOW_ALL_POLICY_NAME);

        Assert.NotNull(allowAllPolicy);
        Assert.NotNull(disallowAllPolicy);
    }

    [Fact]
    public void Configure_NullPoliciesInSettings_ShouldFallback(){
        // Arrange
        var corsSettings =  Options.Create(new CorsSettings());
        var configurator = new EndpointsCorsConfigurator(corsSettings, _logger, _endatixApp, _wildcardSearcher);

        // Act
        configurator.Configure(_options);

        // Assert
        var defaultPolicyName = _options.DefaultPolicyName;

        Assert.NotEmpty(defaultPolicyName);
    }
}
