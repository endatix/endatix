using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// Password hashing for integration-test hosts only. Production hashes at the ASP.NET Core Identity default, 100,000
/// PBKDF2 iterations, about 40 ms a hash; a test world hashes three seeded passwords and every sign-in hashes twice.
/// </summary>
/// <remarks>
/// A hash records its own iteration count, and the hasher asks for a rehash when it reads one below its own. So a
/// user seeded on a fast host and signed in on a production-settings host is rehashed and written, and a refresh token
/// stored on one is rejected on the other: seed and sign in on the same host.
/// </remarks>
public static class IntegrationPasswordHashing
{
    /// <summary>The lowest count the hasher accepts.</summary>
    public const int FAST_ITERATION_COUNT = 1;

    /// <summary>Hashes passwords at <see cref="FAST_ITERATION_COUNT"/>. Never call it outside a test host.</summary>
    public static IServiceCollection UseFastPasswordHashing(this IServiceCollection services) =>
        services.Configure<PasswordHasherOptions>(options => options.IterationCount = FAST_ITERATION_COUNT);
}
