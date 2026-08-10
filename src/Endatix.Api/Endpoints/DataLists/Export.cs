using System.Text.Json;
using Endatix.Api.Endpoints.Common;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.DataLists.GetById;
using Endatix.Core.UseCases.DataLists.Translations;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Endatix.Api.Endpoints.DataLists;

/// <summary>
/// Exports data list items. Format is selected via the JSON body (<c>format</c>: <c>csv</c> | <c>json</c>),
/// mirroring submissions export negotiation.
/// </summary>
public sealed class Export(IMediator mediator) : Endpoint<ExportDataListRequest>
{
    /// <summary>SurveyJS translations CSV.</summary>
    public const string FormatCsv = DataListTransferFormatValidation.FormatCsv;

    /// <summary>JSON array of { value, labels }.</summary>
    public const string FormatJson = DataListTransferFormatValidation.FormatJson;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <inheritdoc />
    public override void Configure()
    {
        Post("data-lists/{dataListId}/export");
        Permissions(Actions.Forms.View);
        Summary(s =>
        {
            s.Summary = "Export data list items";
            s.Description =
                "Exports all items. Set format to 'csv' (translations CSV) or 'json' ({ value, labels } array). Defaults to csv.";
            s.ExampleRequest = new ExportDataListRequest { DataListId = 1, Format = FormatCsv };
            s.Responses[200] = "Items exported successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Data list not found.";
        });
        Description(builder => builder
            .Produces<string>(StatusCodes.Status200OK, "text/csv")
            .Produces<object>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    /// <inheritdoc />
    public override async Task HandleAsync(ExportDataListRequest request, CancellationToken ct)
    {
        var format = NormalizeFormat(request.Format);

        if (string.Equals(format, FormatJson, StringComparison.Ordinal))
        {
            await ExportJsonAsync(request.DataListId, ct);
            return;
        }

        await ExportCsvAsync(request.DataListId, ct);
    }

    internal static string NormalizeFormat(string? format) =>
        DataListTransferFormatValidation.Normalize(format, FormatCsv);

    private async Task ExportCsvAsync(long dataListId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetDataListTranslationsCsvQuery(dataListId), ct);
        if (!result.IsSuccess)
        {
            await Send.ResultAsync(result.ToProblem());
            return;
        }

        HttpContext.Response.Headers.ContentDisposition =
            $"attachment; filename=\"{result.Value.FileName}\"";
        await Send.StringAsync(
            result.Value.Csv,
            contentType: "text/csv; charset=utf-8",
            cancellation: ct);
    }

    private async Task ExportJsonAsync(long dataListId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetDataListByIdQuery(dataListId), ct);
        if (!result.IsSuccess)
        {
            await Send.ResultAsync(result.ToProblem());
            return;
        }

        var payload = result.Value.Items.Select(item => new
        {
            value = item.Value,
            labels = item.Labels
        });

        var json = JsonSerializer.Serialize(payload, _jsonOptions) + "\n";
        var fileName = $"data-list-{dataListId}.json";
        HttpContext.Response.Headers.ContentDisposition = $"attachment; filename=\"{fileName}\"";
        await Send.StringAsync(
            json,
            contentType: "application/json; charset=utf-8",
            cancellation: ct);
    }
}

/// <summary>
/// Validator for <see cref="ExportDataListRequest"/>.
/// </summary>
public sealed class ExportDataListValidator : Validator<ExportDataListRequest>
{
    public ExportDataListValidator()
    {
        RuleFor(x => x.DataListId).GreaterThan(0);
        RuleFor(x => x.Format).IsDataListFileFormat(Export.FormatCsv);
    }
}

/// <summary>
/// Request to export data list items.
/// </summary>
public sealed class ExportDataListRequest
{
    /// <summary>
    /// The data list ID.
    /// </summary>
    public long DataListId { get; init; }

    /// <summary>
    /// Export format: <c>csv</c> (default) or <c>json</c>.
    /// </summary>
    public string? Format { get; init; }
}
