using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// Password hashing for integration-test hosts only. Production hashes at the ASP.NET Core Identity default, 100,000
/// PBKDF2 iterations, about 40 ms a hash; a test world hashes three seeded passwords and every sign-in hashes twice.
/// </summary>
/// <remarks>
/// A hash records its own iteration count, and the hasher asks for a rehash when it reads one below its own. So a
/// password hashed on a fast host and checked on a production-settings host is rehashed and written, and a refresh
/// token stored on a fast host is rejected by a production-settings host. The other way round reads fine. Seed and
/// sign in on the same host.
/// <para>
/// The sign-in's unknown-account path checks against one dummy hash per process, made by the first host that takes
/// that path. With fast and production-settings hosts in one test process, its cost depends on test order.
/// </para>
/// </remarks>
public static class IntegrationPasswordHashing
{
    /// <summary>The lowest count the hasher accepts.</summary>
    public static readonly int FastIterationCount = 1;

    /// <summary>
    /// Hashes passwords at <see cref="FastIterationCount"/>. Call it from a test host's <c>ConfigureTestServices</c>
    /// only: in any other host it weakens every stored password.
    /// </summary>
    public static IServiceCollection UseFastPasswordHashing(this IServiceCollection services) =>
        services.Configure<PasswordHasherOptions>(options => options.IterationCount = FastIterationCount);
}
