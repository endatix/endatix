namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// What an access cache key carries for one request: the deployment prefix, the system version, and the version of
/// the request's tenant (for a principal without a tenant, the version every change moves).
/// </summary>
/// <param name="Deployment">Random per database and never changed, so two deployments sharing one cache never share a key.</param>
/// <param name="System">The version of system roles.</param>
/// <param name="Scope">The tenant's version, or <see cref="AccessVersion.ANY_TENANT_ID"/>'s for a principal without a tenant.</param>
internal readonly record struct AccessVersionKey(Guid Deployment, Guid System, Guid Scope)
{
    /// <summary>The key prefix: the deployment.</summary>
    public string Prefix => Deployment.ToString("N");

    /// <summary>The versions, as one key segment.</summary>
    public string Versions => $"{System:N}{Scope:N}";
}
