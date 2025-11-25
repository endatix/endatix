using Endatix.Framework.Hosting;
using Endatix.Api.Setup;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Endatix.Api.Infrastructure;
using Endatix.Framework.Serialization;
using Ardalis.GuardClauses;
using Microsoft.Extensions.Configuration;
using Endatix.Infrastructure.Identity;
using Microsoft.Extensions.Hosting;
using FastEndpoints.Security;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using NJsonSchema;
using System.Reflection;
using Endatix.Api.Builders;
using Microsoft.Extensions.Logging;
using System;

namespace Endatix.Setup;

/// <summary>
/// Provides extension methods for configuring and setting up the Endatix application
/// </summary>
public static class EndatixAppExtensions
{
    private const int JWT_CLOCK_SKEW_IN_SECONDS = 15;

    /// <summary>
    /// Adds the default JSON options Endatix needs for minimal API results.
    /// </summary>
    private static IServiceCollection AddDefaultJsonOptions(this IServiceCollection services)
    {
        services.Configure<JsonOptions>(options => options.SerializerOptions.Converters.Add(new LongToStringConverter()));

        return services;
    }

    /// <summary>
    /// Formats the Endatix API version string based on the provided Version in the Assembly.
    /// If the version is null, it returns a default version string "unknown".
    /// Otherwise, it formats the version string as "Major.Minor.Build-alpha|beta|rc".
    /// </summary>
    /// <returns>A formatted version string.</returns>
    private static string GetFormattedVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var assemblyVersion = assembly.GetName().Version;
        if (assemblyVersion is null)
        {
            return "unknown";
        }

        return $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}";
    }
}
