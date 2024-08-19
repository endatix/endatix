using Ardalis.GuardClauses;
using Endatix.Api.Infrastructure;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;

namespace Endatix.Setup;

public static class AppBuilderExtensions
{
    private static readonly Action<CorsPolicyBuilder> defaultCorsPolicy = new(b => b.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());

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
