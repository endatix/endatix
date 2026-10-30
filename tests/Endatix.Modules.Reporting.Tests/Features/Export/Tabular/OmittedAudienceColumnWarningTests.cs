using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.Export;
using Endatix.Modules.Reporting.Features.Export.Tabular;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Endatix.Modules.Reporting.Tests.Features.Export.Tabular;

/// <summary>The warning a tabular export logs when a question's export key leaves an audience column out (#1244).</summary>
public sealed class OmittedAudienceColumnWarningTests
{
    private const long TenantId = 1;
    private const long FormId = 100;

    private readonly ILogger<TabularExportDataSource> _logger = Substitute.For<ILogger<TabularExportDataSource>>();
    private readonly List<(LogLevel Level, Dictionary<string, object?> Values)> _entries = [];

    // The logging generator reuses its state object, so the values are read while the call is made.
    public OmittedAudienceColumnWarningTests()
    {
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        _logger
            .When(logger => logger.Log(
                Arg.Any<LogLevel>(),
                Arg.Any<EventId>(),
                Arg.Any<Arg.AnyType>(),
                Arg.Any<Exception?>(),
                Arg.Any<Func<Arg.AnyType, Exception?, string>>()))
            .Do(call => _entries.Add((
                call.ArgAt<LogLevel>(0),
                call.ArgAt<IEnumerable<KeyValuePair<string, object?>>>(2).ToDictionary(StringComparer.Ordinal))));
    }

    [Fact]
    public async Task PrepareOptionsAsync_QuestionTakesAnAudienceKey_LogsOneWarningAndSucceeds()
    {
        // Arrange: with "_" as the separator, multipletext "audience" item "region" exports as "audience_region".
        var dataSource = DataSource();

        // Act
        var result = await dataSource.PrepareOptionsAsync(Context("""{"keySeparator":"_"}"""), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var entry = _entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Warning);
        entry.Values["FormId"].Should().Be(FormId);
        entry.Values["AudienceKeys"].Should().Be("audience_region");
    }

    [Fact]
    public async Task PrepareOptionsAsync_NoQuestionTakesAnAudienceKey_LogsNothing()
    {
        // Arrange: with the default "__" separator the item exports as "audience__region".
        var dataSource = DataSource();

        // Act
        var result = await dataSource.PrepareOptionsAsync(Context(settingsJson: null), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _entries.Should().BeEmpty();
    }

    private TabularExportDataSource DataSource()
    {
        var schemas = Substitute.For<IFormSchemaRepository>();
        schemas.GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(ExportColumnPlanBuilderTests.AudienceRegionCollisionSchema());
        var rows = Substitute.For<IReportingExportRepository>();
        rows.HasExportableRowsAsync(TenantId, FormId, Arg.Any<ExportQueryOptions>(), Arg.Any<CancellationToken>()).Returns(true);
        return new(
            TestExportSchemas.Of(schemas),
            rows,
            new ExportFormatSettingsParser(NullLogger<ExportFormatSettingsParser>.Instance),
            ColumnAliasTransformerRegistry.Default,
            _logger);
    }

    private static ExportDataSourceContext Context(string? settingsJson) =>
        new(
            new ExportDataSourceRequest("csv", typeof(SubmissionExportRow), null),
            TenantId,
            FormId,
            new ExportOptions
            {
                Metadata = new Dictionary<string, object>
                {
                    [SubmissionExportMetadataKeys.ExecutionSettings] = new SubmissionExportExecutionSettings(
                        ExportFormatId: 1,
                        SettingsJson: settingsJson),
                },
            },
            null);
}
