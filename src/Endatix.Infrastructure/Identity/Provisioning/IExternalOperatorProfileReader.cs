namespace Endatix.Infrastructure.Identity.Provisioning;

internal interface IExternalOperatorProfileReader
{
    Task<string?> GetDisplayNameAsync(
        long tenantId,
        string authProvider,
        string externalSubjectId,
        CancellationToken cancellationToken);
}
