using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Submissions.Export;
using System.IO.Pipelines;
using Microsoft.Extensions.Logging;

namespace Endatix.Core.Tests.UseCases.Submissions.Export;

public class SubmissionsExportHandlerTests
{
    private readonly IExportDataSourceResolver _dataSourceResolver;
    private readonly ILogger<SubmissionsExportHandler> _logger;
    private readonly SubmissionsExportHandler _handler;

    public SubmissionsExportHandlerTests()
    {
        _dataSourceResolver = Substitute.For<IExportDataSourceResolver>();
        _logger = Substitute.For<ILogger<SubmissionsExportHandler>>();
        _handler = new SubmissionsExportHandler(_dataSourceResolver, _logger);
    }

    [Fact]
    public async Task Handle_WithSubmissionExportRow_Success()
    {
        // Arrange
        long formId = 1L;
        IExporter exporter = CreateMockExporter(typeof(SubmissionExportRow));
        ExportOptions options = new();
        PipeWriter pipeWriter = new Pipe().Writer;
        FileExport fileExport = new("text/csv", "submissions-1.csv");

        List<SubmissionExportRow> exportRows =
        [
            new() { FormId = formId, Id = 1, AnswersModel = "{}" },
            new() { FormId = formId, Id = 2, AnswersModel = "{}" },
        ];

        SubmissionsExportQuery request = new(
            FormId: formId,
            TenantId: 1L,
            Exporter: exporter,
            Options: options,
            OutputWriter: pipeWriter,
            SqlFunctionName: null);

        ConfigureDataSourceReturningRows(exportRows.Cast<IExportItem>());

        exporter.StreamExportAsync(
            Arg.Any<Func<Type, IAsyncEnumerable<IExportItem>>>(),
            options,
            Arg.Any<CancellationToken>(),
            pipeWriter)
            .Returns(async x =>
            {
                Func<Type, IAsyncEnumerable<IExportItem>> getDataAsync = x.Arg<Func<Type, IAsyncEnumerable<IExportItem>>>();
                await foreach (IExportItem _ in getDataAsync(typeof(SubmissionExportRow))) { }

                return Result.Success(fileExport);
            });

        // Act
        Result<FileExport> result = await _handler.Handle(request, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(fileExport);

        _dataSourceResolver.Received(1).Resolve(Arg.Any<ExportDataSourceRequest>());
    }

    [Fact]
    public async Task Handle_WithDynamicExportRow_Success()
    {
        // Arrange
        long formId = 1L;
        string sqlFunctionName = "custom_export";
        IExporter exporter = CreateMockExporter(typeof(DynamicExportRow));
        ExportOptions options = new();
        PipeWriter pipeWriter = new Pipe().Writer;
        FileExport fileExport = new("application/json", "codebook-1.json");

        List<DynamicExportRow> exportRows =
        [
            new() { Data = "{\"key\":\"value1\"}" },
            new() { Data = "{\"key\":\"value2\"}" },
        ];

        SubmissionsExportQuery request = new(
            FormId: formId,
            TenantId: 1L,
            Exporter: exporter,
            Options: options,
            OutputWriter: pipeWriter,
            SqlFunctionName: sqlFunctionName);

        ConfigureDataSourceReturningRows(exportRows.Cast<IExportItem>());

        SetupExporterToEnumerateData(exporter, typeof(DynamicExportRow), fileExport, options, pipeWriter);

        // Act
        Result<FileExport> result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(fileExport);
    }

    [Fact]
    public async Task Handle_InvalidItemType_ReturnsInvalidResult()
    {
        // Arrange
        long formId = 1L;
        IExporter exporter = CreateMockExporter(typeof(Form));
        ExportOptions options = new();
        PipeWriter pipeWriter = new Pipe().Writer;

        SubmissionsExportQuery request = new(
            FormId: formId,
            TenantId: 1L,
            Exporter: exporter,
            Options: options,
            OutputWriter: pipeWriter,
            SqlFunctionName: null);

        // Act
        Result<FileExport> result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().HaveCount(1);
        result.ValidationErrors.First().ErrorMessage.Should().Contain("Invalid item type");

        _dataSourceResolver.DidNotReceive().Resolve(Arg.Any<ExportDataSourceRequest>());
    }

    [Fact]
    public async Task Handle_StreamExportAsyncFails_ReturnsErrorResult()
    {
        // Arrange
        long formId = 1L;
        IExporter exporter = CreateMockExporter(typeof(SubmissionExportRow));
        ExportOptions options = new();
        PipeWriter pipeWriter = new Pipe().Writer;

        List<SubmissionExportRow> exportRows =
        [
            new() { FormId = formId, Id = 1, AnswersModel = "{}" },
        ];

        SubmissionsExportQuery request = new(
            FormId: formId,
            TenantId: 1L,
            Exporter: exporter,
            Options: options,
            OutputWriter: pipeWriter,
            SqlFunctionName: null);

        ConfigureDataSourceReturningRows(exportRows.Cast<IExportItem>());

        SetupExporterToReturnError(exporter, "Export streaming failed", options, pipeWriter);

        // Act
        Result<FileExport> result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Error);
        result.Errors.Should().Contain("Export streaming failed");
    }

    [Fact]
    public async Task Handle_RepositoryThrowsException_ReturnsErrorResult()
    {
        // Arrange
        long formId = 1L;
        IExporter exporter = CreateMockExporter(typeof(SubmissionExportRow));
        ExportOptions options = new();
        PipeWriter pipeWriter = new Pipe().Writer;

        SubmissionsExportQuery request = new(
            FormId: formId,
            TenantId: 1L,
            Exporter: exporter,
            Options: options,
            OutputWriter: pipeWriter,
            SqlFunctionName: null);

        ConfigureDataSourceThrowing(new Exception("Database connection failed"));

        SetupExporterToHandleException(exporter, typeof(SubmissionExportRow), options, pipeWriter);

        // Act
        Result<FileExport> result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Error);
        result.Errors.Should().Contain(e => e.Contains("Export failed"));
    }

    [Fact]
    public async Task Handle_UnsupportedItemType_ThrowsException()
    {
        // Arrange
        long formId = 1L;
        Type unsupportedType = typeof(CustomExportItem);
        IExporter exporter = CreateMockExporter(unsupportedType);
        ExportOptions options = new();
        PipeWriter pipeWriter = new Pipe().Writer;

        SubmissionsExportQuery request = new(
            FormId: formId,
            TenantId: 1L,
            Exporter: exporter,
            Options: options,
            OutputWriter: pipeWriter,
            SqlFunctionName: null);

        IExportDataSource unsupportedDataSource = Substitute.For<IExportDataSource>();
        unsupportedDataSource.PrepareOptionsAsync(Arg.Any<ExportDataSourceContext>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(options));
        unsupportedDataSource.StreamAsync(Arg.Any<ExportDataSourceContext>(), Arg.Any<CancellationToken>())
            .Returns(x => throw new InvalidOperationException($"Unsupported export item type: {unsupportedType.Name}"));
        _dataSourceResolver.Resolve(Arg.Any<ExportDataSourceRequest>()).Returns(unsupportedDataSource);

        SetupExporterToHandleException(exporter, unsupportedType, options, pipeWriter);

        // Act
        Result<FileExport> result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Error);
        result.Errors.Should().Contain(e => e.Contains("Export failed"));
    }

    [Fact]
    public async Task Handle_PassesCorrectOptionsToExporter()
    {
        // Arrange
        long formId = 1L;
        IExporter exporter = CreateMockExporter(typeof(SubmissionExportRow));
        ExportOptions options = new()
        {
            Columns = ["Id", "FormId"],
            Metadata = new Dictionary<string, object> { ["Key"] = "Value" },
        };
        PipeWriter pipeWriter = new Pipe().Writer;
        FileExport fileExport = new("text/csv", "submissions-1.csv");

        List<SubmissionExportRow> exportRows =
        [
            new() { FormId = formId, Id = 1, AnswersModel = "{}" },
        ];

        SubmissionsExportQuery request = new(
            FormId: formId,
            TenantId: 1L,
            Exporter: exporter,
            Options: options,
            OutputWriter: pipeWriter,
            SqlFunctionName: null);

        ConfigureDataSourceReturningRows(exportRows.Cast<IExportItem>());

        exporter.StreamExportAsync(
            Arg.Any<Func<Type, IAsyncEnumerable<IExportItem>>>(),
            Arg.Is<ExportOptions>(o =>
                o.Columns != null &&
                o.Columns.Contains("Id") &&
                o.Metadata != null &&
                o.Metadata.ContainsKey("Key")),
            Arg.Any<CancellationToken>(),
            pipeWriter)
            .Returns(async x =>
            {
                Func<Type, IAsyncEnumerable<IExportItem>> getDataAsync = x.Arg<Func<Type, IAsyncEnumerable<IExportItem>>>();
                await foreach (IExportItem _ in getDataAsync(typeof(SubmissionExportRow))) { }

                return Result.Success(fileExport);
            });

        // Act
        Result<FileExport> result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await exporter.Received(1).StreamExportAsync(
            Arg.Any<Func<Type, IAsyncEnumerable<IExportItem>>>(),
            Arg.Is<ExportOptions>(o =>
                o.Columns != null &&
                o.Columns.Contains("Id") &&
                o.Metadata != null &&
                o.Metadata.ContainsKey("Key")),
            Arg.Any<CancellationToken>(),
            pipeWriter);
    }

    [Fact]
    public async Task Handle_WithCustomSqlFunctionName_PassesToRepository()
    {
        // Arrange
        long formId = 1L;
        string sqlFunctionName = "custom_export_function";
        IExporter exporter = CreateMockExporter(typeof(SubmissionExportRow));
        ExportOptions options = new();
        PipeWriter pipeWriter = new Pipe().Writer;
        FileExport fileExport = new("text/csv", "submissions-1.csv");

        List<SubmissionExportRow> exportRows =
        [
            new() { FormId = formId, Id = 1, AnswersModel = "{}" },
        ];

        SubmissionsExportQuery request = new(
            FormId: formId,
            TenantId: 1L,
            Exporter: exporter,
            Options: options,
            OutputWriter: pipeWriter,
            SqlFunctionName: sqlFunctionName);

        ConfigureDataSourceReturningRows(exportRows.Cast<IExportItem>());

        SetupExporterToEnumerateData(exporter, typeof(SubmissionExportRow), fileExport, options, pipeWriter);

        // Act
        Result<FileExport> result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithTabularExportDataSource_StreamsRowsAndInjectsColumnPlan()
    {
        // Arrange
        long formId = 1L;
        long tenantId = 42L;
        IExportDataSource tabularDataSource = Substitute.For<IExportDataSource>();
        IExporter exporter = CreateMockExporter(typeof(SubmissionExportRow));
        ExportOptions options = new();
        PipeWriter pipeWriter = new Pipe().Writer;
        FileExport fileExport = new("text/csv", "submissions-1.csv");
        SubmissionExportColumnPlan columnPlan = new(
        [
            new SubmissionExportColumnPlanEntry("qText", "qText", "Simple", "Full Name", "text"),
        ]);

        List<SubmissionExportRow> exportRows =
        [
            new() { FormId = formId, Id = 1, AnswersModel = "{\"qText\":\"John\"}" },
        ];

        SubmissionsExportQuery request = new(
            FormId: formId,
            TenantId: tenantId,
            Exporter: exporter,
            Options: options,
            OutputWriter: pipeWriter,
            SqlFunctionName: null);

        _dataSourceResolver.Resolve(Arg.Any<ExportDataSourceRequest>()).Returns(tabularDataSource);
        tabularDataSource.PrepareOptionsAsync(Arg.Any<ExportDataSourceContext>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                ExportDataSourceContext context = callInfo.Arg<ExportDataSourceContext>();
                context.Options.Metadata ??= new Dictionary<string, object>();
                context.Options.Metadata[SubmissionExportMetadataKeys.ColumnPlan] = columnPlan;
                return Result.Success(context.Options);
            });
        tabularDataSource.StreamAsync(Arg.Any<ExportDataSourceContext>(), Arg.Any<CancellationToken>())
            .Returns(exportRows.Cast<IExportItem>().ToAsyncEnumerable());

        ExportOptions? capturedOptions = null;
        exporter.StreamExportAsync(
            Arg.Any<Func<Type, IAsyncEnumerable<IExportItem>>>(),
            Arg.Do<ExportOptions>(o => capturedOptions = o),
            Arg.Any<CancellationToken>(),
            pipeWriter)
            .Returns(async x =>
            {
                Func<Type, IAsyncEnumerable<IExportItem>> getDataAsync = x.Arg<Func<Type, IAsyncEnumerable<IExportItem>>>();
                await foreach (IExportItem _ in getDataAsync(typeof(SubmissionExportRow))) { }

                return Result.Success(fileExport);
            });

        // Act
        Result<FileExport> result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedOptions.Should().NotBeNull();
        capturedOptions!.Metadata.Should().ContainKey(SubmissionExportMetadataKeys.ColumnPlan);
        capturedOptions.Metadata![SubmissionExportMetadataKeys.ColumnPlan].Should().Be(columnPlan);

        await tabularDataSource.Received(1).PrepareOptionsAsync(Arg.Any<ExportDataSourceContext>(), Arg.Any<CancellationToken>());
        tabularDataSource.Received(1).StreamAsync(Arg.Any<ExportDataSourceContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithShojiCodebookDataSource_StreamsGeneratedCodebook()
    {
        // Arrange
        long formId = 1L;
        long tenantId = 42L;
        IExportDataSource shojiDataSource = Substitute.For<IExportDataSource>();
        IExporter exporter = CreateMockExporter("codebook", typeof(DynamicExportRow));
        ExportOptions options = new();
        PipeWriter pipeWriter = new Pipe().Writer;
        FileExport fileExport = new("application/json", "codebook-1.json");
        const string codebookJson = "{\"format\":\"shoji\"}";

        SubmissionsExportQuery request = new(
            FormId: formId,
            TenantId: tenantId,
            Exporter: exporter,
            Options: options,
            OutputWriter: pipeWriter,
            SqlFunctionName: null);

        _dataSourceResolver.Resolve(Arg.Any<ExportDataSourceRequest>()).Returns(shojiDataSource);
        shojiDataSource.PrepareOptionsAsync(Arg.Any<ExportDataSourceContext>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(options));
        shojiDataSource.StreamAsync(Arg.Any<ExportDataSourceContext>(), Arg.Any<CancellationToken>())
            .Returns(new[] { new DynamicExportRow { Data = codebookJson } }.ToAsyncEnumerable());

        Func<Type, IAsyncEnumerable<IExportItem>>? capturedGetDataAsync = null;
        exporter.StreamExportAsync(
            Arg.Do<Func<Type, IAsyncEnumerable<IExportItem>>>(f => capturedGetDataAsync = f),
            Arg.Any<ExportOptions>(),
            Arg.Any<CancellationToken>(),
            pipeWriter)
            .Returns(Result.Success(fileExport));

        // Act
        Result<FileExport> result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedGetDataAsync.Should().NotBeNull();

        List<IExportItem> items = await capturedGetDataAsync!(typeof(DynamicExportRow)).ToListAsync();
        items.Should().ContainSingle().Which.Should().BeOfType<DynamicExportRow>()
            .Which.Data.Should().Be(codebookJson);

        shojiDataSource.Received(1).StreamAsync(Arg.Any<ExportDataSourceContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_GetDataAsyncFunction_ReturnsCorrectItems()
    {
        // Arrange
        long formId = 1L;
        IExporter exporter = CreateMockExporter(typeof(SubmissionExportRow));
        ExportOptions options = new();
        PipeWriter pipeWriter = new Pipe().Writer;
        FileExport fileExport = new("text/csv", "submissions-1.csv");

        List<SubmissionExportRow> exportRows =
        [
            new() { FormId = formId, Id = 1, AnswersModel = "{}" },
            new() { FormId = formId, Id = 2, AnswersModel = "{}" },
        ];

        SubmissionsExportQuery request = new(
            FormId: formId,
            TenantId: 1L,
            Exporter: exporter,
            Options: options,
            OutputWriter: pipeWriter,
            SqlFunctionName: null);

        ConfigureDataSourceReturningRows(exportRows.Cast<IExportItem>());

        Func<Type, IAsyncEnumerable<IExportItem>>? capturedGetDataAsync = null;
        exporter.StreamExportAsync(
            Arg.Do<Func<Type, IAsyncEnumerable<IExportItem>>>(f => capturedGetDataAsync = f),
            options,
            Arg.Any<CancellationToken>(),
            pipeWriter)
            .Returns(Result.Success(fileExport));

        // Act
        Result<FileExport> result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedGetDataAsync.Should().NotBeNull();

        List<IExportItem> items = await capturedGetDataAsync!(typeof(SubmissionExportRow)).ToListAsync();
        items.Should().HaveCount(2);
        items.Should().AllBeOfType<SubmissionExportRow>();
    }

    private void ConfigureDataSourceReturningRows(IEnumerable<IExportItem> rows)
    {
        IExportDataSource dataSource = Substitute.For<IExportDataSource>();
        dataSource.PrepareOptionsAsync(Arg.Any<ExportDataSourceContext>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Result.Success(callInfo.Arg<ExportDataSourceContext>().Options));
        dataSource.StreamAsync(Arg.Any<ExportDataSourceContext>(), Arg.Any<CancellationToken>())
            .Returns(rows.ToAsyncEnumerable());
        _dataSourceResolver.Resolve(Arg.Any<ExportDataSourceRequest>()).Returns(dataSource);
    }

    private void ConfigureDataSourceThrowing(Exception exception)
    {
        IExportDataSource dataSource = Substitute.For<IExportDataSource>();
        dataSource.PrepareOptionsAsync(Arg.Any<ExportDataSourceContext>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Result.Success(callInfo.Arg<ExportDataSourceContext>().Options));
        dataSource.StreamAsync(Arg.Any<ExportDataSourceContext>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw exception);
        _dataSourceResolver.Resolve(Arg.Any<ExportDataSourceRequest>()).Returns(dataSource);
    }

    private static IExporter CreateMockExporter(Type itemType)
    {
        IExporter exporter = Substitute.For<IExporter>();
        exporter.ItemType.Returns(itemType);
        exporter.Format.Returns("test-format");
        return exporter;
    }

    private static IExporter CreateMockExporter(string format, Type itemType)
    {
        IExporter exporter = Substitute.For<IExporter>();
        exporter.ItemType.Returns(itemType);
        exporter.Format.Returns(format);
        return exporter;
    }

    private static void SetupExporterToEnumerateData(
        IExporter exporter,
        Type expectedItemType,
        FileExport fileExport,
        ExportOptions? options = null,
        PipeWriter? pipeWriter = null)
    {
        exporter.StreamExportAsync(
            Arg.Any<Func<Type, IAsyncEnumerable<IExportItem>>>(),
            options ?? Arg.Any<ExportOptions>(),
            Arg.Any<CancellationToken>(),
            pipeWriter ?? Arg.Any<PipeWriter>())
            .Returns(async x =>
            {
                Func<Type, IAsyncEnumerable<IExportItem>> getDataAsync = x.Arg<Func<Type, IAsyncEnumerable<IExportItem>>>();
                await foreach (IExportItem _ in getDataAsync(expectedItemType)) { }

                return Result.Success(fileExport);
            });
    }

    private static void SetupExporterToReturnError(
        IExporter exporter,
        string errorMessage,
        ExportOptions? options = null,
        PipeWriter? pipeWriter = null)
    {
        exporter.StreamExportAsync(
            Arg.Any<Func<Type, IAsyncEnumerable<IExportItem>>>(),
            options ?? Arg.Any<ExportOptions>(),
            Arg.Any<CancellationToken>(),
            pipeWriter ?? Arg.Any<PipeWriter>())
            .Returns(Result<FileExport>.Error(errorMessage));
    }

    private static void SetupExporterToHandleException(
        IExporter exporter,
        Type expectedItemType,
        ExportOptions? options = null,
        PipeWriter? pipeWriter = null)
    {
        exporter.StreamExportAsync(
            Arg.Any<Func<Type, IAsyncEnumerable<IExportItem>>>(),
            options ?? Arg.Any<ExportOptions>(),
            Arg.Any<CancellationToken>(),
            pipeWriter ?? Arg.Any<PipeWriter>())
            .Returns(async x =>
            {
                Func<Type, IAsyncEnumerable<IExportItem>> getDataAsync = x.Arg<Func<Type, IAsyncEnumerable<IExportItem>>>();
                try
                {
                    await foreach (IExportItem _ in getDataAsync(expectedItemType)) { }
                }
                catch (Exception ex)
                {
                    return Result<FileExport>.Error($"Export failed: {ex.Message}");
                }

                return Result<FileExport>.Error("Export failed: Unexpected error");
            });
    }

    private sealed class CustomExportItem : IExportItem
    {
    }
}

internal static class TestExtensions
{
    public static IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source) =>
        new AsyncEnumerableWrapper<T>(source);

    public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source)
    {
        List<T> list = [];
        await foreach (T item in source)
        {
            list.Add(item);
        }

        return list;
    }

    private sealed class AsyncEnumerableWrapper<T>(IEnumerable<T> source) : IAsyncEnumerable<T>
    {
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
            new AsyncEnumeratorWrapper<T>(source.GetEnumerator());
    }

    private sealed class AsyncEnumeratorWrapper<T>(IEnumerator<T> enumerator) : IAsyncEnumerator<T>
    {
        public T Current => enumerator.Current;

        public ValueTask<bool> MoveNextAsync() => new(enumerator.MoveNext());

        public ValueTask DisposeAsync()
        {
            enumerator.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
