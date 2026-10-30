using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.Export;
using Endatix.Modules.Reporting.Features.Export.Tabular;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.Export.Tabular;

/// <summary>The warnings a tabular export logs when a question's export key leaves a column out (#1244).</summary>
public sealed class OmittedColumnWarningTests
{
    private const long TenantId = 1;
    private const long FormId = 100;

    private readonly ILogger<TabularExportDataSource> _logger = Substitute.For<ILogger<TabularExportDataSource>>();
    private readonly List<(LogLevel Level, Dictionary<string, object?> Values)> _entries = [];

    // The logging generator reuses its state object, so the values are read while the call is made.
    public OmittedColumnWarningTests()
    {
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        _logger
            .WhenForAnyArgs(logger => logger.Log<Arg.AnyType>(default, default, default!, default, default!))
            .Do(call => _entries.Add((
                call.ArgAt<LogLevel>(0),
                call.ArgAt<IEnumerable<KeyValuePair<string, object?>>>(2).ToDictionary(StringComparer.Ordinal))));
    }

    [Theory]
    [InlineData("csv", null)]
    [InlineData("csv-shoji", null)]
    [InlineData("csv-shoji", "qName")]
    public async Task PrepareOptionsAsync_QuestionTakesAnAudienceKey_LogsOneWarningAndSucceeds(string format, string? scopedQuestion)
    {
        // Arrange: with "_" as the separator, multipletext "audience" item "region" exports as "audience_region".
        var dataSource = DataSource(ExportColumnPlanBuilderTests.AudienceRegionCollisionSchema());

        // Act
        var result = await dataSource.PrepareOptionsAsync(
            Context(format, """{"keySeparator":"_"}""", scopedQuestion),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var entry = _entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Warning);
        entry.Values["FormId"].Should().Be(FormId);
        entry.Values["AudienceKeys"].Should().Be("audience_region");
    }

    [Theory]
    [InlineData("csv", null, null)]
    [InlineData("csv", """{"keySeparator":"_"}""", "qName")]
    public async Task PrepareOptionsAsync_NoQuestionTakesAnAudienceKey_LogsNothing(string format, string? settingsJson, string? scopedQuestion)
    {
        // Arrange: with "__" the item exports as "audience__region"; a plain export scoped to "qName" has no codebook
        // that gives "audience_region" to the question.
        var dataSource = DataSource(ExportColumnPlanBuilderTests.AudienceRegionCollisionSchema());

        // Act
        var result = await dataSource.PrepareOptionsAsync(
            Context(format, settingsJson, scopedQuestion),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _entries.Should().BeEmpty();
    }

    [Fact]
    public async Task PrepareOptionsAsync_QuestionTakesASubmitterKey_LogsOneWarningAndSucceeds()
    {
        // Arrange: with "_" as the separator, multipletext "submitter" item "region" exports as "submitter_region".
        var dataSource = DataSource(ExportColumnPlanBuilderTests.SubmitterRegionCollisionSchema());

        // Act
        var result = await dataSource.PrepareOptionsAsync(
            Context("csv-shoji", """{"keySeparator":"_"}""", "qName"),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var entry = _entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Warning);
        entry.Values["FormId"].Should().Be(FormId);
        entry.Values["SubmitterProfileKeys"].Should().Be("submitter_region");
    }

    private TabularExportDataSource DataSource(FormSchemaEntity schema)
    {
        var schemas = Substitute.For<IFormSchemaRepository>();
        schemas.GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>()).Returns(schema);
        var rows = Substitute.For<IReportingExportRepository>();
        rows.HasExportableRowsAsync(TenantId, FormId, Arg.Any<ExportQueryOptions>(), Arg.Any<CancellationToken>()).Returns(true);
        return new(
            TestExportSchemas.Of(schemas),
            rows,
            new ExportFormatSettingsParser(NullLogger<ExportFormatSettingsParser>.Instance),
            ColumnAliasTransformerRegistry.Default,
            _logger);
    }

    private static ExportDataSourceContext Context(string format, string? settingsJson, string? scopedQuestion) =>
        new(
            new ExportDataSourceRequest(format, typeof(SubmissionExportRow), null),
            TenantId,
            FormId,
            new ExportOptions
            {
                Metadata = new Dictionary<string, object>
                {
                    [SubmissionExportMetadataKeys.ExecutionSettings] = new SubmissionExportExecutionSettings(
                        ExportFormatId: 1,
                        SettingsJson: settingsJson,
                        ColumnScope: scopedQuestion is null ? null : [scopedQuestion]),
                },
            },
            null);
}
