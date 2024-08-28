using Endatix.Framework.Hosting;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Endatix.Api.Infrastructure.Cors;

public class EndpointsCorsConfigurator(IOptions<CorsSettings> corsSettings, ILogger<EndpointsCorsConfigurator> logger, IAppEnvironment appEnvironment, IWildcardSearcher wildcardSearcher) : IConfigureOptions<CorsOptions>
{
    public const string ALLOW_ALL_POLICY_NAME = "AllowAll";

    public const string DISALLOW_ALL_POLICY_NAME = "DisallowAll";

    private readonly string[] _no_input_params = [];

    public void Configure(CorsOptions options)
    {
        AddDefaultPolicies(options);

        var settingsValue = corsSettings.Value;
        if (!settingsValue.CorsPolicies.Any())
        {
            var isDevelopment = appEnvironment.IsDevelopment();
            options.DefaultPolicyName = isDevelopment ? ALLOW_ALL_POLICY_NAME : DISALLOW_ALL_POLICY_NAME;
            return;
        }

        foreach (var policySetting in settingsValue.CorsPolicies)
        {
            options.AddPolicy(
                policySetting.PolicyName,
                policy => BuildFrom(policy, policySetting)
            );
        }

        var defaultPolicyName = settingsValue.DefaultPolicyName ?? string.Empty;
        var defaultPolicyExists = string.IsNullOrEmpty(defaultPolicyName) == false && options.GetPolicy(defaultPolicyName) != null;
        options.DefaultPolicyName = defaultPolicyExists ? defaultPolicyName : settingsValue?.CorsPolicies[0].PolicyName;
    }

    private CorsPolicyBuilder BuildFrom(CorsPolicyBuilder builder, CorsPolicySetting policySetting)
    {
        AddAllowedHeaders(builder, policySetting.AllowedHeaders);
        AddAllowedMethods(builder, policySetting.AllowedMethods);
        var wildcardSearchResult = AddAllowedOrigins(builder, policySetting.AllowedOrigins);

        var includeAnyOrigin = wildcardSearchResult == CorsWildcardResult.MatchAll;
        if (includeAnyOrigin && policySetting.AllowCredentials)
        {
            logger.LogWarning("Ignoring {setting} and disallowing credentials. Details: {details}", "AllowCredentials", "The CORS protocol does not allow specifying a wildcard (any) origin and credentials at the same time. Configure the CORS policy by listing individual origins if credentials needs to be supported.");
            _ = builder.DisallowCredentials();
            return builder;
        }

        _ = policySetting.AllowCredentials ? builder.AllowCredentials() : builder.DisallowCredentials();

        if (policySetting.ExposedHeaders?.Count > 0)
        {
            _ = builder.WithExposedHeaders([.. policySetting.ExposedHeaders]);
        }

        if (policySetting.PreflightMaxAgeInSeconds > 0)
        {
            _ = builder.SetPreflightMaxAge(TimeSpan.FromSeconds(policySetting.PreflightMaxAgeInSeconds));
        }

        return builder;
    }

    private CorsWildcardResult AddAllowedOrigins(CorsPolicyBuilder builder, IList<string> allowedOrigins)
    {
        if (allowedOrigins == null || allowedOrigins.Count == 0)
        {
            _ = builder.WithOrigins(_no_input_params);
            return CorsWildcardResult.None;
        }

        var wildcardSearchResult = wildcardSearcher.SearchForWildcard(allowedOrigins);
        _ = wildcardSearchResult switch
        {
            CorsWildcardResult.MatchAll => builder.AllowAnyOrigin(),
            CorsWildcardResult.IgnoreAll => builder.WithOrigins(_no_input_params),
            _ => builder.WithOrigins([.. allowedOrigins])
        };

        return wildcardSearchResult;
    }

    private void AddAllowedHeaders(CorsPolicyBuilder builder, IList<string> allowedHeaders)
    {
        if (allowedHeaders == null || allowedHeaders.Count == 0)
        {
            _ = builder.WithHeaders(_no_input_params);
            return;
        }

        var wildcardSearchResult = wildcardSearcher.SearchForWildcard(allowedHeaders);
        _ = wildcardSearchResult switch
        {
            CorsWildcardResult.MatchAll => builder.AllowAnyHeader(),
            CorsWildcardResult.IgnoreAll => builder.WithHeaders(_no_input_params),
            _ => builder.WithHeaders([.. allowedHeaders])
        };

        return;
    }

    private void AddAllowedMethods(CorsPolicyBuilder builder, IList<string> allowedMethods)
    {
        if (allowedMethods == null || allowedMethods.Count == 0)
        {
            _ = builder.WithMethods(_no_input_params);
            return;
        }

        var wildcardSearchResult = wildcardSearcher.SearchForWildcard(allowedMethods);
        _ = wildcardSearchResult switch
        {
            CorsWildcardResult.MatchAll => builder.AllowAnyMethod(),
            CorsWildcardResult.IgnoreAll => builder.WithMethods(_no_input_params),
            _ => builder.WithMethods([.. allowedMethods])
        };

        return;
    }

    /// <summary>
    /// Adds default CORS policies that will be available for use in the system
    /// </summary>
    /// <param name="options">The <see cref="CorsOptions"/> passed by the DI via the <see cref="IConfigureOptions"/> interface</param>
    private void AddDefaultPolicies(CorsOptions options)
    {
        options.AddPolicy(ALLOW_ALL_POLICY_NAME, policy =>
        {
            _ = policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });

        options.AddPolicy(DISALLOW_ALL_POLICY_NAME, policy =>
        {
            _ = policy.WithOrigins(_no_input_params)
                  .WithMethods(_no_input_params)
                  .WithHeaders(_no_input_params);
        });
    }
}
