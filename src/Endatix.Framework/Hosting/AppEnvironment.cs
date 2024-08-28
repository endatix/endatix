using Ardalis.GuardClauses;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Endatix.Framework.Hosting;

/// <inheritdoc/>
internal class AppEnvironment : IAppEnvironment
{
    private readonly IWebHostEnvironment _environment;

    public AppEnvironment(IWebHostEnvironment environment)
    {
        Guard.Against.Null(environment);

        _environment = environment;
    }

    public string EnvironmentName => _environment.EnvironmentName;

    public bool IsDevelopment() => _environment.IsDevelopment();
}
