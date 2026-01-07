using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Submissions.Export;
using System.IO.Pipelines;
using Microsoft.Extensions.Logging;

namespace Endatix.Core.Tests.UseCases.Submissions.Export;

public class SubmissionsExportHandlerTests
{
    private readonly ISubmissionExportRepository _exportRepository;
    private readonly ILogger<SubmissionsExportHandler> _logger;
    private readonly SubmissionsExportHandler _handler;

    public SubmissionsExportHandlerTests()
    {
        _exportRepository = Substitute.For<ISubmissionExportRepository>();
        _logger = Substitute.For<ILogger<SubmissionsExportHandler>>();
        _handler = new SubmissionsExportHandler(_exportRepository, _logger);
    }

    [Fact]
    public async Task Handle_WithSubmissionExportRow_Success()
    {
        // Arrange
        var formId = 1L;
        var exporter = CreateMockExporter(typeof(SubmissionExportRow));
        var options = new ExportOptions();
        var pipeWriter = new Pipe().Writer;
        var fileExport = new FileExport("text/csv", "submissions-1.csv");

        var exportRows = new List<SubmissionExportRow>
        {
            new() { FormId = formId, Id = 1, AnswersModel = "{}" },
            new() { FormId = formId, Id = 2, AnswersModel = "{}" }
        };

        var request = new SubmissionsExportQuery(
            FormId: formId,
            Exporter: exporter,
            Options: options,
            OutputWriter: pipeWriter,
            SqlFunctionName: null
        );

        _exportRepository.GetExportRowsAsync<SubmissionExportRow>(formId, null, Arg.Any<CancellationToken>())
            .Returns(exportRows.ToAsyncEnumerable());

        exporter.StreamExportAsync(
            Arg.Any<Func<Type, IAsyncEnumerable<IExportItem>>>(),
            options,
            Arg.Any<CancellationToken>(),
            pipeWriter)
            .Returns(async x =>
            {
                // Actually invoke the getDataAsync function to trigger repository call
                var getDataAsync = x.Arg<Func<Type, IAsyncEnumerable<IExportItem>>>();
                var data = getDataAsync(typeof(SubmissionExportRow));
                // Enumerate to trigger the repository call
                await foreach (var _ in data) { }
                return Result.Success(fileExport);
            });

        // Act
        var result = await _handler.Handle(request, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(fileExport);

        _exportRepository.Received(1).GetExportRowsAsync<SubmissionExportRow>(
            formId,
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithDynamicExportRow_Success()
    {
        // Arrange
        var formId = 1L;
        var sqlFunctionName = "custom_export";
        var exporter = CreateMockExporter(typeof(DynamicExportRow));
        var options = new ExportOptions();
        var pipeWriter = new Pipe().Writer;
        var fileExport = new FileExport("application/json", "codebook-1.json");

        var exportRows = new List<DynamicExportRow>
        {
            new() { Data = "{\"key\":\"value1\"}" },
            new() { Data = "{\"key\":\"value2\"}" }
        };

        var request = new SubmissionsExportQuery(
            FormId: formId,
            Exporter: exporter,
            Options: options,
            OutputWriter: pipeWriter,
            SqlFunctionName: sqlFunctionName
        );

        _exportRepository.GetExportRowsAsync<DynamicExportRow>(formId, sqlFunctionName, Arg.Any<CancellationToken>())
            .Returns(exportRows.ToAsyncEnumerable());

        SetupExporterToEnumerateData(exporter, typeof(DynamicExportRow), fileExport, options, pipeWriter);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(fileExport);

        _exportRepository.Received(1).GetExportRowsAsync<DynamicExportRow>(
            formId,
            sqlFunctionName,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InvalidItemType_ReturnsInvalidResult()
    {
        // Arrange
        var formId = 1L;
        var exporter = CreateMockExporter(typeof(Form)); // Form doesn't implement IExportItem
        var options = new ExportOptions();
        var pipeWriter = new Pipe().Writer;

        var request = new SubmissionsExportQuery(
            FormId: formId,
            Exporter: exporter,
            Options: options,
            OutputWriter: pipeWriter,
            SqlFunctionName: null
        );

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().HaveCount(1);
        result.ValidationErrors.First().ErrorMessage.Should().Contain("Invalid item type");

        _exportRepository.DidNotReceive().GetExportRowsAsync<SubmissionExportRow>(
            Arg.Any<long>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_StreamExportAsyncFails_ReturnsErrorResult()
    {
        // Arrange
        var formId = 1L;
        var exporter = CreateMockExporter(typeof(SubmissionExportRow));
        var options = new ExportOptions();
        var pipeWriter = new Pipe().Writer;

        var exportRows = new List<SubmissionExportRow>
        {
            new() { FormId = formId, Id = 1, AnswersModel = "{}" }
        };

        var request = new SubmissionsExportQuery(
            FormId: formId,
            Exporter: exporter,
            Options: options,
            OutputWriter: pipeWriter,
            SqlFunctionName: null
        );

        _exportRepository.GetExportRowsAsync<SubmissionExportRow>(formId, null, Arg.Any<CancellationToken>())
            .Returns(exportRows.ToAsyncEnumerable());

        SetupExporterToReturnError(exporter, "Export streaming failed", options, pipeWriter);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

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
        var formId = 1L;
        var exporter = CreateMockExporter(typeof(SubmissionExportRow));
        var options = new ExportOptions();
        var pipeWriter = new Pipe().Writer;

        var request = new SubmissionsExportQuery(
            FormId: formId,
            Exporter: exporter,
            Options: options,
            OutputWriter: pipeWriter,
            SqlFunctionName: null
        );

        _exportRepository.GetExportRowsAsync<SubmissionExportRow>(formId, null, Arg.Any<CancellationToken>())
            .Returns(x => throw new Exception("Database connection failed"));

        SetupExporterToHandleException(exporter, typeof(SubmissionExportRow), options, pipeWriter);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

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
        var formId = 1L;
        // Create a mock exporter with an unsupported type (not SubmissionExportRow or DynamicExportRow)
        var unsupportedType = typeof(CustomExportItem);
        var exporter = CreateMockExporter(unsupportedType);
        var options = new ExportOptions();
        var pipeWriter = new Pipe().Writer;

        var request = new SubmissionsExportQuery(
            FormId: formId,
            Exporter: exporter,
            Options: options,
            OutputWriter: pipeWriter,
            SqlFunctionName: null
        );

        SetupExporterToHandleException(exporter, unsupportedType, options, pipeWriter);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

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
        var formId = 1L;
        var exporter = CreateMockExporter(typeof(SubmissionExportRow));
        var options = new ExportOptions
        {
            Columns = new List<string> { "Id", "FormId" },
            Metadata = new Dictionary<string, object> { ["Key"] = "Value" }
        };
        var pipeWriter = new Pipe().Writer;
        var fileExport = new FileExport("text/csv", "submissions-1.csv");

        var exportRows = new List<SubmissionExportRow>
        {
            new() { FormId = formId, Id = 1, AnswersModel = "{}" }
        };

        var request = new SubmissionsExportQuery(
            FormId: formId,
            Exporter: exporter,
            Options: options,
            OutputWriter: pipeWriter,
            SqlFunctionName: null
        );

        _exportRepository.GetExportRowsAsync<SubmissionExportRow>(formId, null, Arg.Any<CancellationToken>())
            .Returns(exportRows.ToAsyncEnumerable());

        // Use custom setup to verify options are passed correctly
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
                // Actually invoke the getDataAsync function to trigger repository call
                var getDataAsync = x.Arg<Func<Type, IAsyncEnumerable<IExportItem>>>();
                var data = getDataAsync(typeof(SubmissionExportRow));
                // Enumerate to trigger the repository call
                await foreach (var _ in data) { }
                return Result.Success(fileExport);
            });

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

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
        var formId = 1L;
        var sqlFunctionName = "custom_export_function";
        var exporter = CreateMockExporter(typeof(SubmissionExportRow));
        var options = new ExportOptions();
        var pipeWriter = new Pipe().Writer;
        var fileExport = new FileExport("text/csv", "submissions-1.csv");

        var exportRows = new List<SubmissionExportRow>
        {
            new() { FormId = formId, Id = 1, AnswersModel = "{}" }
        };

        var request = new SubmissionsExportQuery(
            FormId: formId,
            Exporter: exporter,
            Options: options,
            OutputWriter: pipeWriter,
            SqlFunctionName: sqlFunctionName
        );

        _exportRepository.GetExportRowsAsync<SubmissionExportRow>(formId, sqlFunctionName, Arg.Any<CancellationToken>())
            .Returns(exportRows.ToAsyncEnumerable());

        SetupExporterToEnumerateData(exporter, typeof(SubmissionExportRow), fileExport, options, pipeWriter);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _exportRepository.Received(1).GetExportRowsAsync<SubmissionExportRow>(
            formId,
            sqlFunctionName,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_GetDataAsyncFunction_ReturnsCorrectItems()
    {
        // Arrange
        var formId = 1L;
        var exporter = CreateMockExporter(typeof(SubmissionExportRow));
        var options = new ExportOptions();
        var pipeWriter = new Pipe().Writer;
        var fileExport = new FileExport("text/csv", "submissions-1.csv");

        var exportRows = new List<SubmissionExportRow>
        {
            new() { FormId = formId, Id = 1, AnswersModel = "{}" },
            new() { FormId = formId, Id = 2, AnswersModel = "{}" }
        };

        var request = new SubmissionsExportQuery(
            FormId: formId,
            Exporter: exporter,
            Options: options,
            OutputWriter: pipeWriter,
            SqlFunctionName: null
        );

        _exportRepository.GetExportRowsAsync<SubmissionExportRow>(formId, null, Arg.Any<CancellationToken>())
            .Returns(exportRows.ToAsyncEnumerable());

        Func<Type, IAsyncEnumerable<IExportItem>>? capturedGetDataAsync = null;
        exporter.StreamExportAsync(
            Arg.Do<Func<Type, IAsyncEnumerable<IExportItem>>>(f => capturedGetDataAsync = f),
            options,
            Arg.Any<CancellationToken>(),
            pipeWriter)
            .Returns(Result.Success(fileExport));

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedGetDataAsync.Should().NotBeNull();

        // Verify the function returns the correct items
        var items = await capturedGetDataAsync!(typeof(SubmissionExportRow)).ToListAsync();
        items.Should().HaveCount(2);
        items.Should().AllBeOfType<SubmissionExportRow>();
    }

    private static IExporter CreateMockExporter(Type itemType)
    {
        var exporter = Substitute.For<IExporter>();
        exporter.ItemType.Returns(itemType);
        exporter.Format.Returns("test-format");
        return exporter;
    }

    /// <summary>
    /// Sets up the exporter mock to automatically enumerate the data provider function when StreamExportAsync is called.
    /// This ensures repository calls are triggered during testing.
    /// </summary>
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
                // Automatically invoke and enumerate the getDataAsync function to trigger repository calls
                var getDataAsync = x.Arg<Func<Type, IAsyncEnumerable<IExportItem>>>();
                var data = getDataAsync(expectedItemType);
                await foreach (var _ in data) { }
                return Result.Success(fileExport);
            });
    }

    /// <summary>
    /// Sets up the exporter mock to return an error result.
    /// </summary>
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

    /// <summary>
    /// Sets up the exporter mock to handle exceptions during data enumeration.
    /// </summary>
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
                var getDataAsync = x.Arg<Func<Type, IAsyncEnumerable<IExportItem>>>();
                try
                {
                    var data = getDataAsync(expectedItemType);
                    // Enumerate to trigger any exceptions
                    await foreach (var _ in data) { }
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

// Extension methods for test helpers
internal static class TestExtensions
{
    public static IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        return new AsyncEnumerableWrapper<T>(source);
    }

    public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source)
        {
            list.Add(item);
        }
        return list;
    }

    private sealed class AsyncEnumerableWrapper<T> : IAsyncEnumerable<T>
    {
        private readonly IEnumerable<T> _source;

        public AsyncEnumerableWrapper(IEnumerable<T> source)
        {
            _source = source;
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new AsyncEnumeratorWrapper<T>(_source.GetEnumerator());
        }
    }

    private sealed class AsyncEnumeratorWrapper<T> : IAsyncEnumerator<T>
    {
        private readonly IEnumerator<T> _enumerator;

        public AsyncEnumeratorWrapper(IEnumerator<T> enumerator)
        {
            _enumerator = enumerator;
        }

        public T Current => _enumerator.Current;

        public ValueTask<bool> MoveNextAsync()
        {
            return new ValueTask<bool>(_enumerator.MoveNext());
        }

        public ValueTask DisposeAsync()
        {
            _enumerator.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
