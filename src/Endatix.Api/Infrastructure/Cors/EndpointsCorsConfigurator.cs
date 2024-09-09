using Endatix.Framework.Hosting;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Endatix.Api.Infrastructure.Cors;

public class EndpointsCorsConfigurator(IOptions<CorsSettings> corsSettings, ILogger<EndpointsCorsConfigurator> logger, IAppEnvironment appEnvironment, IWildcardSearcher wildcardSearcher) : IConfigureOptions<CorsOptions>
{
    public const string ALLOW_ALL_POLICY_NAME = "AllowAll";

    public const string DISALLOW_ALL_POLICY_NAME = "DisallowAll";

    private readonly string[] _noInputParams = [];

    public void Configure(CorsOptions options)
    {
        AddPredefinedPolicies(options);

        var settingsValue = corsSettings?.Value;
        var isDefaultPolicySet = false;

        if (settingsValue?.CorsPolicies is { Count: > 0 } corsPolicies)
        {
            foreach (var policySetting in corsPolicies)
            {
                options.AddPolicy(
                    policySetting.PolicyName,
                    policy => BuildFrom(policy, policySetting)
                );

                if (settingsValue.DefaultPolicyName == policySetting.PolicyName)
                {
                    options.DefaultPolicyName = policySetting.PolicyName;
                    isDefaultPolicySet = true;
                }
            }

            if (!isDefaultPolicySet)
            {
                options.DefaultPolicyName = corsPolicies[0].PolicyName;
                isDefaultPolicySet = true;
            }
        }

        if (!isDefaultPolicySet)
        {
            var isDevelopment = appEnvironment.IsDevelopment();
            options.DefaultPolicyName = isDevelopment ? ALLOW_ALL_POLICY_NAME : DISALLOW_ALL_POLICY_NAME;
            isDefaultPolicySet = true;
        }

        logger.LogDebug("Default Cors Policy is: {@policy}", options.GetPolicy(options.DefaultPolicyName));
    }

    private CorsPolicyBuilder BuildFrom(CorsPolicyBuilder builder, CorsPolicySettings policySetting)
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

    private CorsWildcardResult AddAllowedOrigins(CorsPolicyBuilder builder, IList<string> allowedOrigins) => AddValuesOrApplyWildcard(
            builder,
            allowedOrigins,
            (builder, allowedOrigins) => builder.WithOrigins(allowedOrigins),
            (builder) => builder.AllowAnyOrigin()
         );

    private void AddAllowedHeaders(CorsPolicyBuilder builder, IList<string> allowedHeaders) => AddValuesOrApplyWildcard(
            builder,
            allowedHeaders,
            (builder, allowedHeaders) => builder.WithHeaders(allowedHeaders),
            (builder) => builder.AllowAnyHeader()
         );

    private void AddAllowedMethods(CorsPolicyBuilder builder, IList<string> allowedMethods) => AddValuesOrApplyWildcard(
            builder,
            allowedMethods,
            (builder, allowedMethods) => builder.WithMethods(allowedMethods),
            (builder) => builder.AllowAnyMethod()
         );

    private CorsWildcardResult AddValuesOrApplyWildcard(
    CorsPolicyBuilder builder,
    IList<string> values,
    Func<CorsPolicyBuilder, string[], CorsPolicyBuilder> withSpecificValues,
    Func<CorsPolicyBuilder, CorsPolicyBuilder> allowAny)
    {
        if (values is null or [])
        {
            _ = withSpecificValues(builder, _noInputParams);
            return CorsWildcardResult.None;
        }

        var wildcardSearchResult = wildcardSearcher.SearchForWildcard(values);
        _ = wildcardSearchResult switch
        {
            CorsWildcardResult.MatchAll => allowAny(builder),
            CorsWildcardResult.IgnoreAll => withSpecificValues(builder, _noInputParams),
            _ => withSpecificValues(builder, [.. values])
        };

        return wildcardSearchResult;
        ;
    }

    /// <summary>
    /// Adds predefined CORS policies that will be available for use in the system
    /// </summary>
    /// <param name="options">The <see cref="CorsOptions"/> passed by the DI via the <see cref="IConfigureOptions"/> interface</param>
    private void AddPredefinedPolicies(CorsOptions options)
    {
        options.AddPolicy(ALLOW_ALL_POLICY_NAME, policy =>
        {
            _ = policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });

        options.AddPolicy(DISALLOW_ALL_POLICY_NAME, policy =>
        {
            _ = policy.WithOrigins(_noInputParams)
                  .WithMethods(_noInputParams)
                  .WithHeaders(_noInputParams);
        });
    }
}
