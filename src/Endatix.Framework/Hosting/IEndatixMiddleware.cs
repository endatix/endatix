using Microsoft.AspNetCore.Builder;

namespace Endatix.Framework.Hosting;

/// <summary>
/// Implementation of this interface will be used to facilitate middleware configuration during WebApplicationBuilding process
/// </summary>
public interface IEndatixMiddleware
{
    /// <summary>
    /// The <see cref="WebApplication"/> instance on which the middleware is, e.g. <code>app.UseMiddlewareMethodName()...</code>
    /// </summary>
    WebApplication App { get; }
}
