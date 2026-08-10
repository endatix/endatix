using Endatix.Api.Endpoints.DataLists;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.DataLists;
using Endatix.Core.UseCases.DataLists.GetById;
using Endatix.Core.UseCases.DataLists.Translations;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Endatix.Api.Tests.Endpoints.DataLists;

public class ExportTests
{
    private const string SampleCsv = "value,default,es\r\napple,Apple,Manzana\r\n";

    private readonly IMediator _mediator = Substitute.For<IMediator>();

    private static DataListDto DataList() => new(
        1,
        "Cities",
        null,
        DateTime.UtcNow,
        null,
        true,
        1,
        "en",
        ["es"],
        [new DataListItemDto(
            10,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["default"] = "Apple",
                ["es"] = "Manzana"
            },
            "apple",
            "Apple")]);

    [Theory]
    [InlineData(null, Export.FormatCsv)]
    [InlineData("", Export.FormatCsv)]
    [InlineData("  ", Export.FormatCsv)]
    [InlineData("CSV", Export.FormatCsv)]
    [InlineData(" json ", Export.FormatJson)]
    public void NormalizeFormat_TrimsLowercasesAndDefaultsToCsv(string? format, string expected)
    {
        Export.NormalizeFormat(format).Should().Be(expected);
    }

    [Fact]
    public async Task HandleAsync_Csv_ReturnsCsvAttachment()
    {
        DefaultHttpContext httpContext = new();
        Export endpoint = Factory.Create<Export>(httpContext, _mediator);
        _mediator.Send(Arg.Any<GetDataListTranslationsCsvQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new DataListTranslationsCsvDto(SampleCsv, "cities-translations.csv")));

        await endpoint.HandleAsync(
            new ExportDataListRequest { DataListId = 1, Format = Export.FormatCsv },
            TestContext.Current.CancellationToken);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        httpContext.Response.Headers.ContentDisposition.ToString()
            .Should().Contain("cities-translations.csv");
        httpContext.Response.ContentType.Should().StartWith("text/csv");
    }

    [Fact]
    public async Task HandleAsync_DefaultFormat_SendsTranslationsCsvQuery()
    {
        DefaultHttpContext httpContext = new();
        Export endpoint = Factory.Create<Export>(httpContext, _mediator);
        _mediator.Send(Arg.Any<GetDataListTranslationsCsvQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new DataListTranslationsCsvDto(SampleCsv, "cities-translations.csv")));

        await endpoint.HandleAsync(
            new ExportDataListRequest { DataListId = 42, Format = null },
            TestContext.Current.CancellationToken);

        await _mediator.Received(1).Send(
            Arg.Is<GetDataListTranslationsCsvQuery>(q => q.DataListId == 42),
            Arg.Any<CancellationToken>());
        await _mediator.DidNotReceive().Send(
            Arg.Any<GetDataListByIdQuery>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Csv_MapsRequestToTranslationsCsvQuery()
    {
        DefaultHttpContext httpContext = new();
        Export endpoint = Factory.Create<Export>(httpContext, _mediator);
        _mediator.Send(Arg.Any<GetDataListTranslationsCsvQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new DataListTranslationsCsvDto(SampleCsv, "cities-translations.csv")));

        await endpoint.HandleAsync(
            new ExportDataListRequest { DataListId = 123, Format = Export.FormatCsv },
            TestContext.Current.CancellationToken);

        await _mediator.Received(1).Send(
            Arg.Is<GetDataListTranslationsCsvQuery>(q => q.DataListId == 123),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Csv_DataListNotFound_ReturnsProblem()
    {
        DefaultHttpContext httpContext = new();
        Export endpoint = Factory.Create<Export>(httpContext, _mediator);
        _mediator.Send(Arg.Any<GetDataListTranslationsCsvQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<DataListTranslationsCsvDto>.NotFound("Data list not found."));

        await endpoint.HandleAsync(
            new ExportDataListRequest { DataListId = 1, Format = Export.FormatCsv },
            TestContext.Current.CancellationToken);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task HandleAsync_Json_ReturnsJsonAttachment()
    {
        DefaultHttpContext httpContext = new();
        Export endpoint = Factory.Create<Export>(httpContext, _mediator);
        _mediator.Send(Arg.Any<GetDataListByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(DataList()));

        await endpoint.HandleAsync(
            new ExportDataListRequest { DataListId = 1, Format = Export.FormatJson },
            TestContext.Current.CancellationToken);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        httpContext.Response.Headers.ContentDisposition.ToString()
            .Should().Contain("data-list-1.json");
        httpContext.Response.ContentType.Should().StartWith("application/json");
    }

    [Fact]
    public async Task HandleAsync_Json_MapsRequestToGetByIdQuery()
    {
        DefaultHttpContext httpContext = new();
        Export endpoint = Factory.Create<Export>(httpContext, _mediator);
        _mediator.Send(Arg.Any<GetDataListByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(DataList()));

        await endpoint.HandleAsync(
            new ExportDataListRequest { DataListId = 99, Format = Export.FormatJson },
            TestContext.Current.CancellationToken);

        await _mediator.Received(1).Send(
            Arg.Is<GetDataListByIdQuery>(q => q.DataListId == 99),
            Arg.Any<CancellationToken>());
        await _mediator.DidNotReceive().Send(
            Arg.Any<GetDataListTranslationsCsvQuery>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Json_DataListNotFound_ReturnsProblem()
    {
        DefaultHttpContext httpContext = new();
        Export endpoint = Factory.Create<Export>(httpContext, _mediator);
        _mediator.Send(Arg.Any<GetDataListByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<DataListDto>.NotFound("Data list not found."));

        await endpoint.HandleAsync(
            new ExportDataListRequest { DataListId = 1, Format = Export.FormatJson },
            TestContext.Current.CancellationToken);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}
