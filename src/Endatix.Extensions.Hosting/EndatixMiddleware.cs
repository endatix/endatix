using Ardalis.GuardClauses;
using Endatix.Framework.Hosting;
using Microsoft.AspNetCore.Builder;

namespace Endatix;

/// <inheritdoc/>
public class EndatixMiddleware : IEndatixMiddleware
{
    private readonly WebApplication _app;


    /// <inheritdoc/>
    public EndatixMiddleware(WebApplication app)
    {
        Guard.Against.Null(app);

        _app = app;
    }
    public WebApplication App => _app;
}
