using System.Security.Claims;
using Ardalis.GuardClauses;
using Endatix.Api.Infrastructure;
using Endatix.Framework.Hosting;
using Endatix.Infrastructure.Identity;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace Endatix.Setup;

/// <summary>
/// Provides extension methods for configuring middleware in the Endatix application.
/// </summary>
public static class MiddlewareExtensions
{
    /// <summary>
    /// Configures the Endatix API middleware. This includes setting up CORS with the specified policy or a default policy that allows any origin, header, and method. The default CORS policy is too permissive and NOT recommended for Production. Consider adding a custom policy whitelisting a limited set of allowed origins
    /// </summary>
    /// <param name="endatixMiddleware">The <see cref="IEndatixMiddleware"/> instance to configure.</param>
    /// <returns>The <see cref="IEndatixMiddleware"/> instance after the middleware is configured.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the <paramref name="endatixMiddleware"/> is null.</exception>
    public static IEndatixMiddleware UseEndatixApi(this IEndatixMiddleware endatixMiddleware)
    {
        Guard.Against.Null(endatixMiddleware);

        var app = endatixMiddleware.App;

        app.UseDefaultExceptionHandler(app.Logger, true, true);
        app.UseFastEndpoints(fastEndpoints =>
        {
            fastEndpoints.Versioning.Prefix = "v";
            fastEndpoints.Endpoints.RoutePrefix = "api";
            fastEndpoints.Serializer.Options.Converters.Add(new LongToStringConverter());
            fastEndpoints.Security.RoleClaimType = ClaimTypes.Role;
            fastEndpoints.Security.PermissionsClaimType = ClaimNames.Permission;
        });

        if (endatixMiddleware.App.Environment.IsDevelopment() || endatixMiddleware.App.Environment.IsProduction())
        {
            app.UseSwaggerGen(null, c => c.Path = "");
        }

        app.UseCors();

        return endatixMiddleware;
    }
}
