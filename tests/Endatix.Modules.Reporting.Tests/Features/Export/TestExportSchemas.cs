using Endatix.Core.Abstractions.Submissions;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.Export;
using NSubstitute;

namespace Endatix.Modules.Reporting.Tests.Features.Export;

/// <summary>Export schemas over a substitute repository, with the audience properties an export would read.</summary>
internal static class TestExportSchemas
{
    public static ExportSchemas Of(IFormSchemaRepository schemas, params PersonalizationVariableInfo[] variables) =>
        new(schemas, Catalog(variables));

    public static IPersonalizationCatalog Catalog(params PersonalizationVariableInfo[] variables)
    {
        var catalog = Substitute.For<IPersonalizationCatalog>();
        catalog.ListForFormAsync(default, default, default)
            .ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<PersonalizationVariableInfo>>(variables));
        return catalog;
    }
}
