using Endatix.Api.Endpoints.DataLists;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.DataLists;
using Endatix.Core.UseCases.DataLists.ReplaceItems;
using Endatix.Core.UseCases.DataLists.Translations;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.DataLists;

public class ImportTests
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
    [InlineData(null, Import.FormatJson)]
    [InlineData("", Import.FormatJson)]
    [InlineData("  ", Import.FormatJson)]
    [InlineData("JSON", Import.FormatJson)]
    [InlineData(" csv ", Import.FormatCsv)]
    public void NormalizeFormat_TrimsLowercasesAndDefaultsToJson(string? format, string expected)
    {
        Import.NormalizeFormat(format).Should().Be(expected);
    }

    [Fact]
    public async Task ExecuteAsync_Json_ReturnsDetailsAndMapsReplaceItemsCommand()
    {
        Import endpoint = Factory.Create<Import>(_mediator);
        _mediator.Send(Arg.Any<ReplaceDataListItemsCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(DataList()));

        var response = await endpoint.ExecuteAsync(
            new ImportDataListRequest
            {
                DataListId = 1,
                Format = Import.FormatJson,
                Items =
                [
                    new ImportDataListItemRequest
                    {
                        Labels = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["default"] = "Apple",
                            ["es"] = "Manzana"
                        },
                        Value = "apple"
                    }
                ]
            },
            TestContext.Current.CancellationToken);

        var ok = response.Result.Should().BeOfType<Ok<DataListDetailsModel>>().Subject;
        ok.Value!.Items.Should().ContainSingle(i => i.Value == "apple");
        await _mediator.Received(1).Send(
            Arg.Is<ReplaceDataListItemsCommand>(c =>
                c.DataListId == 1
                && c.Items.Count == 1
                && c.Items.Single().Value == "apple"),
            Arg.Any<CancellationToken>());
        await _mediator.DidNotReceive().Send(
            Arg.Any<ReplaceDataListTranslationsCsvCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_DefaultFormat_SendsReplaceItemsCommand()
    {
        Import endpoint = Factory.Create<Import>(_mediator);
        _mediator.Send(Arg.Any<ReplaceDataListItemsCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(DataList()));

        await endpoint.ExecuteAsync(
            new ImportDataListRequest
            {
                DataListId = 42,
                Format = null,
                Items =
                [
                    new ImportDataListItemRequest { Label = "Apple", Value = "apple" }
                ]
            },
            TestContext.Current.CancellationToken);

        await _mediator.Received(1).Send(
            Arg.Is<ReplaceDataListItemsCommand>(c =>
                c.DataListId == 42
                && c.Items.Single().Label == "Apple"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_Json_WithEnsureLocales_ForwardsThemToCommand()
    {
        Import endpoint = Factory.Create<Import>(_mediator);
        _mediator.Send(Arg.Any<ReplaceDataListItemsCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(DataList()));

        await endpoint.ExecuteAsync(
            new ImportDataListRequest
            {
                DataListId = 1,
                Format = Import.FormatJson,
                Items = [new ImportDataListItemRequest { Label = "Apple", Value = "apple" }],
                EnsureLocales = ["fr", "es"]
            },
            TestContext.Current.CancellationToken);

        await _mediator.Received(1).Send(
            Arg.Is<ReplaceDataListItemsCommand>(c =>
                c.EnsureLocales.SequenceEqual(new[] { "fr", "es" })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_Json_DataListNotFound_ReturnsProblem()
    {
        Import endpoint = Factory.Create<Import>(_mediator);
        _mediator.Send(Arg.Any<ReplaceDataListItemsCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<DataListDto>.NotFound("Data list not found."));

        var response = await endpoint.ExecuteAsync(
            new ImportDataListRequest
            {
                DataListId = 1,
                Format = Import.FormatJson,
                Items = [new ImportDataListItemRequest { Label = "Apple", Value = "apple" }]
            },
            TestContext.Current.CancellationToken);

        var problem = response.Result.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_Json_Invalid_ReturnsProblem()
    {
        Import endpoint = Factory.Create<Import>(_mediator);
        _mediator.Send(Arg.Any<ReplaceDataListItemsCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<DataListDto>.Invalid(new ValidationError
            {
                Identifier = "Items[0].Labels",
                ErrorMessage = "Labels (or legacy Label) is required."
            }));

        var response = await endpoint.ExecuteAsync(
            new ImportDataListRequest
            {
                DataListId = 1,
                Format = Import.FormatJson,
                Items = [new ImportDataListItemRequest { Value = "apple" }]
            },
            TestContext.Current.CancellationToken);

        var problem = response.Result.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ExecuteAsync_Csv_ReturnsDetailsAndMapsTranslationsCsvCommand()
    {
        Import endpoint = Factory.Create<Import>(_mediator);
        _mediator.Send(Arg.Any<ReplaceDataListTranslationsCsvCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(DataList()));

        var response = await endpoint.ExecuteAsync(
            new ImportDataListRequest
            {
                DataListId = 1,
                Format = Import.FormatCsv,
                Csv = SampleCsv
            },
            TestContext.Current.CancellationToken);

        response.Result.Should().BeOfType<Ok<DataListDetailsModel>>();
        await _mediator.Received(1).Send(
            Arg.Is<ReplaceDataListTranslationsCsvCommand>(c => c.DataListId == 1 && c.Csv == SampleCsv),
            Arg.Any<CancellationToken>());
        await _mediator.DidNotReceive().Send(
            Arg.Any<ReplaceDataListItemsCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_Csv_WithEnsureLocales_ForwardsThemToCommand()
    {
        Import endpoint = Factory.Create<Import>(_mediator);
        _mediator.Send(Arg.Any<ReplaceDataListTranslationsCsvCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(DataList()));

        await endpoint.ExecuteAsync(
            new ImportDataListRequest
            {
                DataListId = 1,
                Format = Import.FormatCsv,
                Csv = SampleCsv,
                EnsureLocales = ["fr", "es"]
            },
            TestContext.Current.CancellationToken);

        await _mediator.Received(1).Send(
            Arg.Is<ReplaceDataListTranslationsCsvCommand>(c =>
                c.EnsureLocales.SequenceEqual(new[] { "fr", "es" })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_Csv_DataListNotFound_ReturnsProblem()
    {
        Import endpoint = Factory.Create<Import>(_mediator);
        _mediator.Send(Arg.Any<ReplaceDataListTranslationsCsvCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<DataListDto>.NotFound("Data list not found."));

        var response = await endpoint.ExecuteAsync(
            new ImportDataListRequest
            {
                DataListId = 1,
                Format = Import.FormatCsv,
                Csv = SampleCsv
            },
            TestContext.Current.CancellationToken);

        var problem = response.Result.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_Csv_Invalid_ReturnsProblem()
    {
        Import endpoint = Factory.Create<Import>(_mediator);
        _mediator.Send(Arg.Any<ReplaceDataListTranslationsCsvCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<DataListDto>.Invalid(new ValidationError
            {
                Identifier = "Csv",
                ErrorMessage = "The first CSV column must be 'value'."
            }));

        var response = await endpoint.ExecuteAsync(
            new ImportDataListRequest
            {
                DataListId = 1,
                Format = Import.FormatCsv,
                Csv = "bad"
            },
            TestContext.Current.CancellationToken);

        var problem = response.Result.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }
}
