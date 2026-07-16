using Endatix.Modules.Reporting.Contracts.Export;

namespace Endatix.Modules.Reporting.Features.ExportFormats;

/// <summary>
/// Seeds default tenant export formats when a tenant is provisioned.
/// </summary>
public interface IDefaultExportFormatsSeeder
{
    Task SeedAsync(long tenantId, CancellationToken cancellationToken);
}

internal sealed class DefaultExportFormatsSeeder(IExportFormatRepository repository) : IDefaultExportFormatsSeeder
{
    public Task SeedAsync(long tenantId, CancellationToken cancellationToken) =>
        repository.SeedDefaultsAsync(tenantId, cancellationToken);
}
