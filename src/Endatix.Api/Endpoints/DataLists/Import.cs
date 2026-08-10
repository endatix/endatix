using Endatix.Api.Endpoints.Common;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.DataLists.ReplaceItems;
using Endatix.Core.UseCases.DataLists.Translations;
using Endatix.Infrastructure.Data.Config;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.DataLists;

/// <summary>
/// Imports (full-replaces) data list items. Format is selected via the JSON body
/// (<c>format</c>: <c>json</c> | <c>csv</c>), mirroring submissions export negotiation.
/// </summary>
public sealed class Import(IMediator mediator)
    : Endpoint<ImportDataListRequest, Results<Ok<DataListDetailsModel>, ProblemHttpResult>>
{
    /// <summary>JSON items payload.</summary>
    public const string FormatJson = "json";

    /// <summary>CSV string payload (SurveyJS translations shape).</summary>
    public const string FormatCsv = "csv";

    /// <inheritdoc />
    public override void Configure()
    {
        Put("data-lists/{dataListId}/import");
        Permissions(Actions.Forms.Edit);
        Summary(s =>
        {
            s.Summary = "Import data list items";
            s.Description =
                "Replaces all items. Set format to 'json' with items[], or 'csv' with a translations CSV string. " +
                "Optional ensureLocales adds catalog cultures before import.";
            s.ExampleRequest = new ImportDataListRequest
            {
                DataListId = 1,
                Format = FormatJson,
                Items =
                [
                    new ImportDataListItemRequest
                    {
                        Labels = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["default"] = "New York",
                            ["es"] = "Nueva York"
                        },
                        Value = "NYC"
                    }
                ],
                EnsureLocales = ["es"]
            };
            s.Responses[200] = "Items imported successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Data list not found.";
        });
        Description(builder => builder
            .Produces<DataListDetailsModel>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<DataListDetailsModel>, ProblemHttpResult>> ExecuteAsync(
        ImportDataListRequest request,
        CancellationToken ct)
    {
        var format = NormalizeFormat(request.Format);

        if (string.Equals(format, FormatCsv, StringComparison.Ordinal))
        {
            ReplaceDataListTranslationsCsvCommand csvCommand = new(
                request.DataListId,
                request.Csv ?? string.Empty,
                request.EnsureLocales);
            var csvResult = await mediator.Send(csvCommand, ct);
            return TypedResultsBuilder
                .MapResult(csvResult, DataListMapper.MapDetails)
                .SetTypedResults<Ok<DataListDetailsModel>, ProblemHttpResult>();
        }

        ReplaceDataListItemsCommand jsonCommand = new(
            request.DataListId,
            [.. (request.Items ?? []).Select(ToReplaceDataListItemInput)],
            request.EnsureLocales);
        var jsonResult = await mediator.Send(jsonCommand, ct);

        return TypedResultsBuilder
            .MapResult(jsonResult, DataListMapper.MapDetails)
            .SetTypedResults<Ok<DataListDetailsModel>, ProblemHttpResult>();
    }

    internal static string NormalizeFormat(string? format) =>
        string.IsNullOrWhiteSpace(format)
            ? FormatJson
            : format.Trim().ToLowerInvariant();

    private static ReplaceDataListItemInput ToReplaceDataListItemInput(ImportDataListItemRequest request) => new(
        Value: request.Value ?? string.Empty,
        Labels: request.Labels,
        Label: request.Label);
}

/// <summary>
/// Validator for <see cref="ImportDataListRequest"/>.
/// </summary>
public sealed class ImportDataListValidator : Validator<ImportDataListRequest>
{
    /// <summary>
    /// Upper bound for an uploaded CSV document.
    /// </summary>
    public const int MaxCsvLength = 2_000_000;

    public ImportDataListValidator()
    {
        RuleFor(x => x.DataListId).GreaterThan(0);

        RuleFor(x => x.Format)
            .Must(format =>
            {
                var normalized = Import.NormalizeFormat(format);
                return normalized is Import.FormatJson or Import.FormatCsv;
            })
            .WithMessage("Format must be 'json' or 'csv'.");

        RuleFor(x => x.EnsureLocales)
            .IsEnsureLocales()
            .When(x => x.EnsureLocales is { Count: > 0 });

        When(x => Import.NormalizeFormat(x.Format) == Import.FormatCsv, () =>
        {
            RuleFor(x => x.Csv)
                .NotEmpty()
                .WithMessage("A CSV payload is required when format is 'csv'.")
                .MaximumLength(MaxCsvLength);
        });

        When(x => Import.NormalizeFormat(x.Format) == Import.FormatJson, () =>
        {
            RuleFor(x => x.Items).NotNull();
            RuleFor(x => x.Items)
                .Must(items => items!.Count <= DataList.MAX_ITEMS)
                .WithMessage($"A data list cannot have more than {DataList.MAX_ITEMS} items.")
                .When(x => x.Items is not null);

            RuleForEach(x => x.Items).ChildRules(item =>
            {
                item.RuleFor(x => x.Value)
                    .NotEmpty()
                    .MaximumLength(DataSchemaConstants.MAX_NAME_LENGTH);

                item.RuleFor(x => x)
                    .Must(HasLabelsOrLabel)
                    .WithMessage("Either Labels or Label is required.");

                item.RuleFor(x => x.Label)
                    .MaximumLength(DataSchemaConstants.MAX_NAME_LENGTH)
                    .When(x => !string.IsNullOrWhiteSpace(x.Label));

                item.RuleFor(x => x.Labels)
                    .Must(labels => labels is null || labels.Values.All(v =>
                        string.IsNullOrWhiteSpace(v) || v.Trim().Length <= DataListItem.MAX_LABEL_LENGTH))
                    .WithMessage($"Each label value cannot exceed {DataListItem.MAX_LABEL_LENGTH} characters.");
            });
        });
    }

    private static bool HasLabelsOrLabel(ImportDataListItemRequest item) =>
        (item.Labels is not null && item.Labels.Count > 0)
        || !string.IsNullOrWhiteSpace(item.Label);
}

/// <summary>
/// Request to import (replace) data list items.
/// </summary>
public sealed class ImportDataListRequest
{
    /// <summary>
    /// The data list ID.
    /// </summary>
    public long DataListId { get; init; }

    /// <summary>
    /// Import format: <c>json</c> (default) or <c>csv</c>.
    /// </summary>
    public string? Format { get; init; }

    /// <summary>
    /// JSON items when <see cref="Format"/> is <c>json</c>.
    /// </summary>
    public IReadOnlyCollection<ImportDataListItemRequest>? Items { get; init; }

    /// <summary>
    /// RFC 4180 translations CSV when <see cref="Format"/> is <c>csv</c>.
    /// </summary>
    public string? Csv { get; init; }

    /// <summary>
    /// Cultures to add to AvailableLocales before import (idempotent).
    /// </summary>
    public IReadOnlyCollection<string> EnsureLocales { get; init; } = [];
}

/// <summary>
/// A single item in a JSON import payload.
/// </summary>
public sealed class ImportDataListItemRequest
{
    /// <summary>
    /// Localized labels including <c>default</c>. Preferred over <see cref="Label"/>.
    /// </summary>
    public Dictionary<string, string>? Labels { get; init; }

    /// <summary>
    /// Monolingual label shorthand (maps to Labels.default).
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    /// The invariant item value.
    /// </summary>
    public string? Value { get; init; }
}
