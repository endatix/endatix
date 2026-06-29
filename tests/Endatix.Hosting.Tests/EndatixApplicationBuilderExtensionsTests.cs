using Endatix.Api.Builders;
using Endatix.Framework.Configuration;
using Endatix.Hosting.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MicrosoftOptions = Microsoft.Extensions.Options.Options;

namespace Endatix.Hosting.Tests;

public sealed class EndatixApplicationBuilderExtensionsTests
{
    [Fact]
    public async Task UseEndatix_WithOptionsOverride_StartsFromConfigDerivedDefaults()
    {
        var sawConfigDerivedDefaults = false;
        var app = CreateApplicationBuilder(
            new HostingOptions
            {
                ReverseProxy = new ReverseProxyOptions
                {
                    Enabled = true
                }
            },
            new ApiOptions
            {
                UseSwagger = false,
                SwaggerPath = "/docs"
            });

        app.UseEndatix(options =>
        {
            sawConfigDerivedDefaults =
                options.UseForwardedHeaders &&
                !options.UseHsts &&
                !options.UseHttpsRedirection &&
                !options.ApiOptions.UseSwagger &&
                options.ApiOptions.SwaggerPath == "/docs";

            DisableDefaultMiddleware(options);
            options.ConfigureAdditionalMiddleware = builder =>
                builder.Run(context =>
                {
                    context.Response.StatusCode = StatusCodes.Status204NoContent;
                    return Task.CompletedTask;
                });
        });

        var context = CreateHttpContext(app.ApplicationServices);

        await app.Build()(context);

        sawConfigDerivedDefaults.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status204NoContent);
    }

    [Fact]
    public async Task UseEndatix_WithBuilderOverride_UsesManualCompositionOnly()
    {
        var app = CreateApplicationBuilder(new HostingOptions(), new ApiOptions());

        app.UseEndatix(builder =>
        {
            builder.App.Run(context =>
            {
                context.Response.StatusCode = StatusCodes.Status202Accepted;
                return Task.CompletedTask;
            });
        });

        var context = CreateHttpContext(app.ApplicationServices);

        await app.Build()(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status202Accepted);
    }

    private static ApplicationBuilder CreateApplicationBuilder(HostingOptions hostingOptions, ApiOptions apiOptions)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(MicrosoftOptions.Create(hostingOptions));
        services.AddSingleton(MicrosoftOptions.Create(apiOptions));

        return new ApplicationBuilder(services.BuildServiceProvider());
    }

    private static DefaultHttpContext CreateHttpContext(IServiceProvider services)
    {
        return new DefaultHttpContext
        {
            RequestServices = services
        };
    }

    private static void DisableDefaultMiddleware(EndatixMiddlewareOptions options)
    {
        options.UseForwardedHeaders = false;
        options.UseExceptionHandler = false;
        options.UseSecurity = false;
        options.UseMultitenancy = false;
        options.UseHsts = false;
        options.UseHttpsRedirection = false;
        options.UseApi = false;
        options.UseHealthChecks = false;
    }
}
