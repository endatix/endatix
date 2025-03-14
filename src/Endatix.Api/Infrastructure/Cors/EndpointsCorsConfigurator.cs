using Endatix.Framework.Hosting;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Endatix.Api.Infrastructure.Cors;

/// <summary>
/// Configures CORS policies for endpoints.
/// </summary>
public class EndpointsCorsConfigurator : IConfigureOptions<CorsOptions>
{
    public const string ALLOW_ALL_POLICY_NAME = "AllowAll";
    public const string DISALLOW_ALL_POLICY_NAME = "DisallowAll";

    private static readonly string[] _emptyStrings = Array.Empty<string>();

    private readonly IOptions<CorsSettings> _options;
    private readonly ILogger<EndpointsCorsConfigurator> _logger;
    private readonly IAppEnvironment _environment;
    private readonly IWildcardSearcher _wildcardSearcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="EndpointsCorsConfigurator"/> class.
    /// </summary>
    /// <param name="options">The CORS settings options.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="environment">The application environment.</param>
    /// <param name="wildcardSearcher">The wildcard searcher.</param>
    public EndpointsCorsConfigurator(
        IOptions<CorsSettings> options,
        ILogger<EndpointsCorsConfigurator> logger,
        IAppEnvironment environment,
        IWildcardSearcher wildcardSearcher)
    {
        _options = options;
        _logger = logger;
        _environment = environment;
        _wildcardSearcher = wildcardSearcher;
    }

    public void Configure(CorsOptions options)
    {
        AddPredefinedPolicies(options);

        var settingsValue = _options?.Value;
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
        }

        if (!isDefaultPolicySet)
        {
            var isDevelopment = IsDevelopment();
            options.DefaultPolicyName = isDevelopment ? ALLOW_ALL_POLICY_NAME : DISALLOW_ALL_POLICY_NAME;
            isDefaultPolicySet = true;
        }

        _logger.LogDebug("Default Cors Policy is: {@policy}", options.GetPolicy(options.DefaultPolicyName));
    }

    private CorsPolicyBuilder BuildFrom(CorsPolicyBuilder builder, CorsPolicySettings policySetting)
    {
        AddAllowedHeaders(builder, policySetting.AllowedHeaders);
        AddAllowedMethods(builder, policySetting.AllowedMethods);
        var wildcardSearchResult = AddAllowedOrigins(builder, policySetting.AllowedOrigins);

        var includeAnyOrigin = wildcardSearchResult == CorsWildcardResult.MatchAll;
        if (includeAnyOrigin && policySetting.AllowCredentials)
        {
            _logger.LogWarning("Ignoring {setting} and disallowing credentials. Details: {details}", "AllowCredentials", "The CORS protocol does not allow specifying a wildcard (any) origin and credentials at the same time. Configure the CORS policy by listing individual origins if credentials needs to be supported.");
            _ = builder.DisallowCredentials();
            return builder;
        }

        if (policySetting.AllowCredentials)
        {
            builder.AllowCredentials();
        }
        else
        {
            builder.DisallowCredentials();
        }

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
            _ = withSpecificValues(builder, _emptyStrings);
            return CorsWildcardResult.None;
        }

        var wildcardSearchResult = _wildcardSearcher.SearchForWildcard(values);
        _ = wildcardSearchResult switch
        {
            CorsWildcardResult.MatchAll => allowAny(builder),
            CorsWildcardResult.IgnoreAll => withSpecificValues(builder, _emptyStrings),
            _ => withSpecificValues(builder, [.. values])
        };

        return wildcardSearchResult;
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
            _ = policy.WithOrigins(_emptyStrings)
                  .WithMethods(_emptyStrings)
                  .WithHeaders(_emptyStrings);
        });
    }

    /// <summary>
    /// Determines if the environment is development.
    /// </summary>
    /// <returns>True if the environment is development; otherwise, false.</returns>
    private bool IsDevelopment() => _environment.IsDevelopment();
}
