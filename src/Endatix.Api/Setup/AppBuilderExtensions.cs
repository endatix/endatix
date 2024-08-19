using Ardalis.GuardClauses;
using Endatix.Api.Infrastructure;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;

namespace Endatix.Setup;

/// <summary>
/// Provides extension methods for configuring middleware in the Endatix application.
/// </summary>
public static class AppBuilderExtensions
{
    private static readonly Action<CorsPolicyBuilder> defaultCorsPolicy = new(b => b.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());

    /// <summary>
    /// Configures the Endatix API middleware. This includes setting up CORS with the specified policy or a default policy that allows any origin, header, and method. The default CORS policy is too permissive and NOT recommended for Production. Consider adding a custom policy whitelisting a limited set of allowed origins
    /// </summary>
    /// <param name="app">The <see cref="WebApplication"/> instance to configure.</param>
    /// <param name="configurePolicy">Optional. A delegate to configure the CORS policy. 
    /// If null, a default policy that allows any origin, header, and method is applied.</param>
    /// <returns>The <see cref="IApplicationBuilder"/> instance after the middleware is configured.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the <paramref name="app"/> is null.</exception>
    public static IApplicationBuilder UseEndatixApi(this WebApplication app, Action<CorsPolicyBuilder>? configurePolicy = null)
    {
        Guard.Against.Null(app);

        app.UseDefaultExceptionHandler(app.Logger, true, true);
        app.UseFastEndpoints(fastEndpoints =>
        {
            fastEndpoints.Versioning.Prefix = "v";
            fastEndpoints.Endpoints.RoutePrefix = "api";
            fastEndpoints.Serializer.Options.Converters.Add(new LongToStringConverter());
        });
        app.UseSwaggerGen();

        if (configurePolicy == null)
        {

            configurePolicy = defaultCorsPolicy;
        }

        var policyBuilder = new CorsPolicyBuilder();
        configurePolicy(policyBuilder);

        app.UseCors(configurePolicy);

        return app;
    }
}
