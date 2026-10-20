using System.Text;
using System.Text.Json;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.Submissions;
using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.Export;
using Endatix.Modules.Reporting.Features.Export.FormSchema;
using Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;
using Endatix.Modules.Reporting.Features.Export.Tabular;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.Personalization;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.Export;

/// <summary>
/// Each export shows audience columns under the properties' current names, in Audience-tab order, from one catalog
/// read, while the stored schema keeps the names frozen on the first responses.
/// </summary>
public sealed class AudienceNamesAtExportTests
{
    private const long TenantId = 1;
    private const long FormId = 100;
    private const string Definition = """{"pages":[{"name":"p1","elements":[{"type":"text","name":"q1"}]}]}""";

    private static readonly ExportFormatSettingsParser _settingsParser = new(NullLogger<ExportFormatSettingsParser>.Instance);

    // Frozen as "City" and "Age"; since then city was renamed "Town" and moved before age (key order is age, city).
    private static readonly AudiencePropertyName[] _current =
    [
        new("city", "Town", SortOrder: 1),
        new("age", "Age", SortOrder: 2),
    ];

    private readonly IFormSchemaRepository _schemas = Substitute.For<IFormSchemaRepository>();
    private readonly IAudiencePropertyCatalog _catalog = TestExportSchemas.Catalog(_current);

    public AudienceNamesAtExportTests() =>
        _schemas.GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>()).Returns(PersonalizedSchema());

    [Fact]
    public async Task NativeCodebook_RenamedAndReorderedProperties_ShowsCurrentNamesInTheBlockOrder()
    {
        // Arrange
        FormSchemaCodebookExportDataSource dataSource = new(new ExportSchemas(_schemas, _catalog), _settingsParser);

        // Act
        using var codebook = JsonDocument.Parse(await StreamTextAsync(dataSource, Context("codebook", typeof(DynamicExportRow))));

        // Assert
        var root = codebook.RootElement;
        root.GetProperty("columns").GetProperty("audience_city").GetProperty("title").GetProperty("default").GetString()
            .Should().Be("Town");
        root.GetProperty("personalization").GetProperty("variables").EnumerateObject().Select(variable => variable.Name)
            .Should().Equal("city", "age");
        await _catalog.ReceivedWithAnyArgs(1).ListForFormAsync(default, default, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ShojiCodebook_RenamedAndReorderedProperties_ShowsCurrentNamesInTheBlockOrder()
    {
        // Arrange
        ShojiCodebookExportDataSource dataSource = new(
            new ExportSchemas(_schemas, _catalog),
            Substitute.For<IFormsRepository>(),
            _settingsParser);

        // Act
        using var codebook = JsonDocument.Parse(await StreamTextAsync(dataSource, Context("codebook-shoji", typeof(DynamicExportRow))));

        // Assert
        var table = codebook.RootElement.GetProperty("body").GetProperty("table");
        table.GetProperty("metadata").GetProperty("audience_city").GetProperty("name").GetString().Should().Be("Town");
        table.GetProperty("order").EnumerateArray().Select(alias => alias.GetString())
            .Where(alias => alias!.StartsWith("Audience", StringComparison.OrdinalIgnoreCase))
            .Should().Equal("AudienceIdentifier", "audience_city", "audience_age");
        await _catalog.ReceivedWithAnyArgs(1).ListForFormAsync(default, default, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TabularPlan_RenamedAndReorderedProperties_ShowsCurrentNamesInTheBlockOrder()
    {
        // Arrange
        var rows = Substitute.For<IReportingExportRepository>();
        rows.HasExportableRowsAsync(TenantId, FormId, Arg.Any<ExportQueryOptions>(), Arg.Any<CancellationToken>()).Returns(true);
        TabularExportDataSource dataSource = new(
            new ExportSchemas(_schemas, _catalog),
            rows,
            _settingsParser,
            ColumnAliasTransformerRegistry.Default);
        var context = Context("csv", typeof(SubmissionExportRow));

        // Act
        (await dataSource.PrepareOptionsAsync(context, TestContext.Current.CancellationToken)).IsSuccess.Should().BeTrue();

        // Assert
        var plan = (SubmissionExportColumnPlan)context.Options.Metadata![SubmissionExportMetadataKeys.ColumnPlan];
        plan.Columns.Where(column => column.ExportKey.StartsWith("Audience", StringComparison.OrdinalIgnoreCase))
            .Select(column => (column.ExportKey, column.HeaderLabel))
            .Should().Equal(("AudienceIdentifier", "Audience identifier"), ("audience_city", "Town"), ("audience_age", "Age"));
        await _catalog.ReceivedWithAnyArgs(1).ListForFormAsync(default, default, Arg.Any<CancellationToken>());
    }

    private static async Task<string> StreamTextAsync(IExportDataSource dataSource, ExportDataSourceContext context)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        (await dataSource.PrepareOptionsAsync(context, cancellationToken)).IsSuccess.Should().BeTrue();
        StringBuilder text = new();
        await foreach (var item in dataSource.StreamAsync(context, cancellationToken))
        {
            text.Append(((DynamicExportRow)item).Data);
        }

        return text.ToString();
    }

    private static FormSchemaEntity PersonalizedSchema()
    {
        FormSchemaCompiler compiler = new();
        var compiled = compiler.CompilePersisted(Definition);
        compiled = compiler.IncludePersonalization(
            Definition,
            new PersistedSchemaJson(compiled.FlatteningMapJson, compiled.CodebookJson),
            PersonalizationColumns.From(PersonalizationReader.Read("""
                {"identifier":"CG-1","variables":{"city":"Columbus","age":65},
                 "values":[{"variableName":"city","name":"City","dataType":"text"},
                           {"variableName":"age","name":"Age","dataType":"number"}]}
                """)));
        return new FormSchemaEntity(TenantId, FormId, 1, compiled.FlatteningMapJson, compiled.CodebookJson);
    }

    private static ExportDataSourceContext Context(string format, Type itemType) =>
        new(
            new ExportDataSourceRequest(format, itemType, null, ExportFormatId: 1),
            TenantId,
            FormId,
            new ExportOptions(),
            null);
}
