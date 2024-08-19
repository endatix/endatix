using Endatix.Framework.Hosting;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Setup;

public static class EndatixAppExtensions
{
    public static IEndatixApp UseApiEndpoints(this IEndatixApp endatixApp)
    {
        endatixApp.Services
                .AddCors()
                .AddFastEndpoints()
                .SwaggerDocument(o =>
                    {
                        o.ShortSchemaNames = true;
                        o.DocumentSettings = s =>
                        {
                            s.Version = "v0";
                            s.DocumentName = "Internal MVP (Alpha) Release";
                            s.Title = "Endatix API";
                        };
                    });

        return endatixApp;
    }
}
