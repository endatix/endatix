using Endatix.Api.Infrastructure;
using Endatix.Framework.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Endatix.Modules.Jobs.Endpoints;

/// <summary>
/// Registers the MVC controllers generated from the Jobs OpenAPI contract.
/// </summary>
/// <remarks>
/// Controllers live beside FastEndpoints rather than replacing it: the two use separate route
/// tables, so they coexist as long as their paths differ. Everything here exists to make a
/// controller behave the way the rest of the platform already does — MVC's defaults differ from
/// the FastEndpoints pipeline in three ways that are invisible until a response is compared.
/// </remarks>
internal static class JobsControllerRegistration
{
    public static IServiceCollection AddJobsControllers(this IServiceCollection services)
    {
        services
            .AddControllers(options => options.Conventions.Add(
                new HideFromApiExplorerConvention(typeof(JobsControllerRegistration).Assembly)))
            // Controllers ship in this assembly, not the entry assembly, so MVC will not find them
            // by its default scan.
            .AddApplicationPart(typeof(JobsControllerRegistration).Assembly)
            .AddJsonOptions(options =>
            {
                // Snowflake ids exceed the range JavaScript numbers represent exactly. The
                // FastEndpoints serializer already carries this converter; MVC has its own options
                // object and would otherwise emit a number.
                options.JsonSerializerOptions.Converters.Add(new LongToStringConverter());

                // Enum members carry [EnumMember] from the generator, which System.Text.Json
                // ignores. Without this, a Pending job serializes as a number — and one that does
                // not even match the domain enum, which numbers from 0 where the contract numbers
                // from 1.
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            })
            .ConfigureApiBehaviorOptions(options =>
            {
                // MVC's own model-state response is ValidationProblemDetails, which keys messages
                // under `errors`; the platform keys them under `fields`. This is the controller
                // equivalent of the FastEndpoints Errors.ResponseBuilder, and without it the same
                // validation failure reports differently depending on which framework served it.
                options.InvalidModelStateResponseFactory = context =>
                {
                    var fields = context.ModelState
                        .Where(entry => entry.Value?.Errors.Count > 0)
                        .ToDictionary(
                            entry => entry.Key,
                            entry => entry.Value!.Errors.Select(error => error.ErrorMessage).ToArray(),
                            StringComparer.Ordinal);

                    var problem = EndatixProblemDetails.Create(
                        StatusCodes.Status400BadRequest,
                        title: null,
                        detail: string.Join('\n', fields.SelectMany(field => field.Value)),
                        httpContext: context.HttpContext,
                        fields: fields);

                    return new ObjectResult(problem)
                    {
                        StatusCode = StatusCodes.Status400BadRequest,
                        ContentTypes = { "application/problem+json" },
                    };
                };
            });

        return services;
    }
}

/// <summary>
/// Keeps controllers generated from an OpenAPI contract out of the application's own OpenAPI
/// document.
/// </summary>
/// <remarks>
/// The specification these controllers were generated from is published separately, from the
/// contracts repository that owns it. Letting the application re-derive a document from code that
/// was itself generated from that specification would give the same API two descriptions, free to
/// drift, with the generated one looking authoritative because it is closer to the running server.
/// <para>
/// Scoped to one assembly so that hand-written controllers, if any are ever added, still appear.
/// </para>
/// </remarks>
internal sealed class HideFromApiExplorerConvention(Assembly contractAssembly) : IControllerModelConvention
{
    public void Apply(ControllerModel controller)
    {
        if (controller.ControllerType.Assembly != contractAssembly)
        {
            return;
        }

        controller.ApiExplorer.IsVisible = false;

        // [ApiController] makes each action visible in its own right, and the action-level value
        // wins over the controller's, so hiding the controller alone leaves every route in place.
        foreach (var action in controller.Actions)
        {
            action.ApiExplorer.IsVisible = false;
        }
    }
}
