using System.Text.Json;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Exporting;
using Endatix.Infrastructure.Exporting.Exporters.Dynamic;
using Endatix.Infrastructure.Exporting.Exporters.Submissions;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Tests.Exporting;

public sealed class ExporterFactoryTests
{
    private readonly ILogger<SubmissionCsvExporter> _csvLogger;
    private readonly ILogger<SubmissionJsonExporter> _jsonLogger;
    private readonly ILogger<CodebookJsonExporter> _codebookLogger;
    private readonly IEnumerable<IValueTransformer> _globalTransformers;
    private readonly SubmissionCsvExporter _csvExporter;
    private readonly SubmissionJsonExporter _jsonExporter;
    private readonly CodebookJsonExporter _codebookExporter;

    public ExporterFactoryTests()
    {
        _csvLogger = Substitute.For<ILogger<SubmissionCsvExporter>>();
        _jsonLogger = Substitute.For<ILogger<SubmissionJsonExporter>>();
        _codebookLogger = Substitute.For<ILogger<CodebookJsonExporter>>();
        var transformer = Substitute.For<IValueTransformer>();
        transformer.Transform(Arg.Any<object?>(), Arg.Any<TransformationContext<SubmissionExportRow>>()).Returns((object?)null);
        _globalTransformers = new[] { transformer };
        _csvExporter = new SubmissionCsvExporter(_csvLogger, _globalTransformers);
        _jsonExporter = new SubmissionJsonExporter(_jsonLogger, _globalTransformers);
        _codebookExporter = new CodebookJsonExporter(_codebookLogger);
    }

    [Fact]
    public void GetExporter_ShouldReturnCsvExporter_WhenFormatIsCsv()
    {
        // Arrange
        var exporters = new IExporter[] { _csvExporter, _jsonExporter };
        var factory = new ExporterFactory(exporters);

        // Act
        var exporter = factory.GetExporter<SubmissionExportRow>("csv");

        // Assert
        Assert.IsType<SubmissionCsvExporter>(exporter);
        Assert.Equal("csv", exporter.Format);
    }

    [Fact]
    public void GetExporter_ShouldReturnJsonExporter_WhenFormatIsJson()
    {
        // Arrange
        var exporters = new IExporter[] { _csvExporter, _jsonExporter };
        var factory = new ExporterFactory(exporters);

        // Act
        var exporter = factory.GetExporter<SubmissionExportRow>("json");

        // Assert
        Assert.IsType<SubmissionJsonExporter>(exporter);
        Assert.Equal("json", exporter.Format);
    }

    [Fact]
    public void GetExporter_ShouldBeCaseInsensitive()
    {
        // Arrange
        var exporters = new IExporter[] { _csvExporter, _jsonExporter };
        var factory = new ExporterFactory(exporters);

        // Act & Assert
        Assert.IsType<SubmissionCsvExporter>(factory.GetExporter<SubmissionExportRow>("CSV"));
        Assert.IsType<SubmissionCsvExporter>(factory.GetExporter<SubmissionExportRow>("Csv"));
        Assert.IsType<SubmissionJsonExporter>(factory.GetExporter<SubmissionExportRow>("JSON"));
        Assert.IsType<SubmissionJsonExporter>(factory.GetExporter<SubmissionExportRow>("Json"));
    }

    [Fact]
    public void GetExporter_ShouldThrow_WhenFormatNotFound()
    {
        // Arrange
        var exporters = new IExporter[] { _csvExporter, _jsonExporter };
        var factory = new ExporterFactory(exporters);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => factory.GetExporter<SubmissionExportRow>("xlsx"));
        Assert.Contains("No exporter registered", exception.Message);
        Assert.Contains("xlsx", exception.Message);
    }

    [Fact]
    public void GetExporter_ShouldThrow_WhenTypeNotFound()
    {
        // Arrange
        var exporters = new IExporter[] { _csvExporter, _jsonExporter };
        var factory = new ExporterFactory(exporters);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => factory.GetExporter<TestExportItem>("csv"));
        Assert.Contains("No exporter registered", exception.Message);
    }

    [Fact]
    public void GetExporter_ShouldReturnFirstMatch_WhenMultipleExportersForSameFormat()
    {
        // Arrange - Create a duplicate CSV exporter
        var duplicateCsvExporter = new SubmissionCsvExporter(_csvLogger, _globalTransformers);
        var exporters = new IExporter[] { _csvExporter, duplicateCsvExporter, _jsonExporter };
        var factory = new ExporterFactory(exporters);

        // Act
        var exporter = factory.GetExporter<SubmissionExportRow>("csv");

        // Assert
        Assert.NotNull(exporter);
        Assert.IsType<SubmissionCsvExporter>(exporter);
    }

    [Fact]
    public void GetSupportedExporters_ShouldReturnAllExporters_ForSubmissionExportRow()
    {
        // Arrange
        var exporters = new IExporter[] { _csvExporter, _jsonExporter };
        var factory = new ExporterFactory(exporters);

        // Act
        var supportedExporters = factory.GetSupportedExporters<SubmissionExportRow>();

        // Assert
        Assert.Equal(2, supportedExporters.Count);
        Assert.Contains(typeof(SubmissionCsvExporter), supportedExporters);
        Assert.Contains(typeof(SubmissionJsonExporter), supportedExporters);
    }

    [Fact]
    public void GetSupportedExporters_ShouldReturnEmpty_WhenNoExportersForType()
    {
        // Arrange
        var exporters = new IExporter[] { _csvExporter, _jsonExporter };
        var factory = new ExporterFactory(exporters);

        // Act
        var supportedExporters = factory.GetSupportedExporters<TestExportItem>();

        // Assert
        Assert.Empty(supportedExporters);
    }

    [Fact]
    public void GetSupportedExporters_ShouldReturnDistinctTypes()
    {
        // Arrange - Add duplicate exporters
        var duplicateCsv = new SubmissionCsvExporter(_csvLogger, _globalTransformers);
        var exporters = new IExporter[] { _csvExporter, duplicateCsv, _jsonExporter };
        var factory = new ExporterFactory(exporters);

        // Act
        var supportedExporters = factory.GetSupportedExporters<SubmissionExportRow>();

        // Assert
        Assert.Equal(2, supportedExporters.Count); // Should be distinct
    }

    [Fact]
    public void GetSupportedFormats_ShouldReturnAllFormats_ForSubmissionExportRow()
    {
        // Arrange
        var exporters = new IExporter[] { _csvExporter, _jsonExporter };
        var factory = new ExporterFactory(exporters);

        // Act
        var formats = factory.GetSupportedFormats<SubmissionExportRow>();

        // Assert
        Assert.Equal(2, formats.Count);
        Assert.Contains("csv", formats);
        Assert.Contains("json", formats);
    }

    [Fact]
    public void GetSupportedFormats_ShouldReturnDistinctFormats()
    {
        // Arrange - Add duplicate exporters with same format
        var duplicateCsv = new SubmissionCsvExporter(_csvLogger, _globalTransformers);
        var exporters = new IExporter[] { _csvExporter, duplicateCsv, _jsonExporter };
        var factory = new ExporterFactory(exporters);

        // Act
        var formats = factory.GetSupportedFormats<SubmissionExportRow>();

        // Assert
        Assert.Equal(2, formats.Count); // Should be distinct
        Assert.Contains("csv", formats);
        Assert.Contains("json", formats);
    }

    [Fact]
    public void GetSupportedFormats_ShouldReturnEmpty_WhenNoExportersForType()
    {
        // Arrange
        var exporters = new IExporter[] { _csvExporter, _jsonExporter };
        var factory = new ExporterFactory(exporters);

        // Act
        var formats = factory.GetSupportedFormats<TestExportItem>();

        // Assert
        Assert.Empty(formats);
    }

    [Fact]
    public void Constructor_ShouldHandleEmptyExportersList()
    {
        // Arrange & Act
        var exporters = Array.Empty<IExporter>();
        var factory = new ExporterFactory(exporters);

        // Assert
        Assert.Empty(factory.GetSupportedFormats<SubmissionExportRow>());
        Assert.Throws<InvalidOperationException>(() => factory.GetExporter<SubmissionExportRow>("csv"));
    }

    #region GetExporter(string format, Type itemType) Tests

    [Fact]
    public void GetExporter_WithFormatAndType_ShouldReturnCsvExporter_WhenFormatIsCsv()
    {
        // Arrange
        var exporters = new IExporter[] { _csvExporter, _jsonExporter };
        var factory = new ExporterFactory(exporters);

        // Act
        var exporter = factory.GetExporter("csv", typeof(SubmissionExportRow));

        // Assert
        Assert.IsType<SubmissionCsvExporter>(exporter);
        Assert.Equal("csv", exporter.Format);
        Assert.Equal(typeof(SubmissionExportRow), exporter.ItemType);
    }

    [Fact]
    public void GetExporter_WithFormatAndType_ShouldReturnJsonExporter_WhenFormatIsJson()
    {
        // Arrange
        var exporters = new IExporter[] { _csvExporter, _jsonExporter };
        var factory = new ExporterFactory(exporters);

        // Act
        var exporter = factory.GetExporter("json", typeof(SubmissionExportRow));

        // Assert
        Assert.IsType<SubmissionJsonExporter>(exporter);
        Assert.Equal("json", exporter.Format);
        Assert.Equal(typeof(SubmissionExportRow), exporter.ItemType);
    }

    [Fact]
    public void GetExporter_WithFormatAndType_ShouldReturnCodebookExporter_WhenFormatIsCodebook()
    {
        // Arrange
        var exporters = new IExporter[] { _csvExporter, _jsonExporter, _codebookExporter };
        var factory = new ExporterFactory(exporters);

        // Act
        var exporter = factory.GetExporter("codebook", typeof(DynamicExportRow));

        // Assert
        Assert.IsType<CodebookJsonExporter>(exporter);
        Assert.Equal("codebook", exporter.Format);
        Assert.Equal(typeof(DynamicExportRow), exporter.ItemType);
    }

    [Fact]
    public void GetExporter_WithFormatAndType_ShouldBeCaseInsensitive()
    {
        // Arrange
        var exporters = new IExporter[] { _csvExporter, _jsonExporter };
        var factory = new ExporterFactory(exporters);

        // Act & Assert
        Assert.IsType<SubmissionCsvExporter>(factory.GetExporter("CSV", typeof(SubmissionExportRow)));
        Assert.IsType<SubmissionCsvExporter>(factory.GetExporter("Csv", typeof(SubmissionExportRow)));
        Assert.IsType<SubmissionJsonExporter>(factory.GetExporter("JSON", typeof(SubmissionExportRow)));
        Assert.IsType<SubmissionJsonExporter>(factory.GetExporter("Json", typeof(SubmissionExportRow)));
    }

    [Fact]
    public void GetExporter_WithFormatAndType_ShouldThrow_WhenFormatNotFound()
    {
        // Arrange
        var exporters = new IExporter[] { _csvExporter, _jsonExporter };
        var factory = new ExporterFactory(exporters);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => factory.GetExporter("xlsx", typeof(SubmissionExportRow)));
        Assert.Contains("No exporter registered", exception.Message);
        Assert.Contains("xlsx", exception.Message);
        Assert.Contains("SubmissionExportRow", exception.Message);
    }

    [Fact]
    public void GetExporter_WithFormatAndType_ShouldThrow_WhenTypeNotFound()
    {
        // Arrange
        var exporters = new IExporter[] { _csvExporter, _jsonExporter };
        var factory = new ExporterFactory(exporters);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => factory.GetExporter("csv", typeof(TestExportItem)));
        Assert.Contains("No exporter registered", exception.Message);
        Assert.Contains("csv", exception.Message);
        Assert.Contains("TestExportItem", exception.Message);
    }

    [Fact]
    public void GetExporter_WithFormatAndType_ShouldThrow_WhenFormatMatchesButTypeDoesNot()
    {
        // Arrange
        var exporters = new IExporter[] { _csvExporter, _jsonExporter, _codebookExporter };
        var factory = new ExporterFactory(exporters);

        // Act & Assert
        // "codebook" format exists but for DynamicExportRow, not SubmissionExportRow
        var exception = Assert.Throws<InvalidOperationException>(
            () => factory.GetExporter("codebook", typeof(SubmissionExportRow)));
        Assert.Contains("No exporter registered", exception.Message);
        Assert.Contains("codebook", exception.Message);
        Assert.Contains("SubmissionExportRow", exception.Message);
    }

    [Fact]
    public void GetExporter_WithFormatAndType_ShouldReturnFirstMatch_WhenMultipleExportersForSameFormatAndType()
    {
        // Arrange - Create a duplicate CSV exporter
        var duplicateCsvExporter = new SubmissionCsvExporter(_csvLogger, _globalTransformers);
        var exporters = new IExporter[] { _csvExporter, duplicateCsvExporter, _jsonExporter };
        var factory = new ExporterFactory(exporters);

        // Act
        var exporter = factory.GetExporter("csv", typeof(SubmissionExportRow));

        // Assert
        Assert.NotNull(exporter);
        Assert.IsType<SubmissionCsvExporter>(exporter);
        Assert.Equal("csv", exporter.Format);
        Assert.Equal(typeof(SubmissionExportRow), exporter.ItemType);
    }

    [Fact]
    public void GetExporter_WithFormatAndType_ShouldDistinguishBetweenDifferentTypesWithSameFormat()
    {
        // Arrange
        // Note: In practice, we might have different exporters for different types with same format
        // For now, we test that the method correctly matches both format and type
        var exporters = new IExporter[] { _csvExporter, _jsonExporter, _codebookExporter };
        var factory = new ExporterFactory(exporters);

        // Act
        var submissionExporter = factory.GetExporter("csv", typeof(SubmissionExportRow));
        var codebookExporter = factory.GetExporter("codebook", typeof(DynamicExportRow));

        // Assert
        Assert.IsType<SubmissionCsvExporter>(submissionExporter);
        Assert.Equal(typeof(SubmissionExportRow), submissionExporter.ItemType);

        Assert.IsType<CodebookJsonExporter>(codebookExporter);
        Assert.Equal(typeof(DynamicExportRow), codebookExporter.ItemType);
    }

    #endregion

    private class TestExportItem : IExportItem
    {
    }
}

