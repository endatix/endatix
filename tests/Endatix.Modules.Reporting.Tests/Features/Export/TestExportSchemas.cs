using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.Export;
using NSubstitute;

namespace Endatix.Modules.Reporting.Tests.Features.Export;

/// <summary>Export schemas over a substitute repository, with the audience properties an export would read.</summary>
internal static class TestExportSchemas
{
    public static ExportSchemas Of(IFormSchemaRepository schemas, params AudiencePropertyName[] variables) =>
        new(schemas, Catalog(variables));

    public static IAudiencePropertyCatalog Catalog(params AudiencePropertyName[] variables)
    {
        var catalog = Substitute.For<IAudiencePropertyCatalog>();
        catalog.ListForFormAsync(default, default, default)
            .ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<AudiencePropertyName>>(variables));
        return catalog;
    }
}
