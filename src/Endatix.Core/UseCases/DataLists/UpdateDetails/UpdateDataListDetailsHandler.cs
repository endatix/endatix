using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.DataLists.Create;
using MediatR;

namespace Endatix.Core.UseCases.DataLists.UpdateDetails;

/// <summary>
/// Handler for <see cref="UpdateDataListDetailsCommand"/>.
/// </summary>
[DirectCallOnly]
public sealed class UpdateDataListDetailsHandler : ICommandHandler<UpdateDataListDetailsCommand, Result<DataListDto>>
{
    public const string DuplicateNameErrorCode = CreateDataListHandler.DuplicateNameErrorCode;

    private readonly DataListRows _lists;
    private readonly IValueNormalizer _valueNormalizer;
    private readonly DataListDetailsWriter _writer;

    /// <summary>Uses the supplied repositories for direct calls.</summary>
    public UpdateDataListDetailsHandler(IRepository<DataList> repository,
        IValueNormalizer valueNormalizer,
        IUniqueConstraintViolationChecker uniqueConstraintViolationChecker,
        IMediator mediator)
    {
        _lists = new DataListRows(repository, ContentQueryScope.Repository);
        _valueNormalizer = valueNormalizer;
        _writer = new DataListDetailsWriter(_lists, uniqueConstraintViolationChecker, mediator);
    }

    private UpdateDataListDetailsHandler(DataListRows lists, IValueNormalizer valueNormalizer, DataListDetailsWriter writer)
    {
        _lists = lists;
        _valueNormalizer = valueNormalizer;
        _writer = writer;
    }

    /// <inheritdoc />
    public async Task<Result<DataListDto>> Handle(
        UpdateDataListDetailsCommand request,
        CancellationToken cancellationToken)
    {
        var dataList = await _lists.FindAsync(request.DataListId, cancellationToken);
        if (dataList is null)
        {
            return Result.NotFound("Data list not found.");
        }

        var nextName = request.Name is null ? dataList.Name : request.Name.Trim();
        if (string.IsNullOrWhiteSpace(nextName))
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(UpdateDataListDetailsCommand.Name),
                ErrorMessage = "Name is required."
            });
        }

        // Matches CreateDataListHandler's `request.Description?.Trim()`: a
        // whitespace-only description normalizes to "" (not null), so the
        // same logical "empty description" value is stored identically
        // regardless of which endpoint wrote it.
        var nextDescription = request.Description is null
            ? dataList.Description
            : request.Description.Trim();

        var normalizedName = _valueNormalizer.Normalize(nextName);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return Result.Error("Data list name could not be normalized.");
        }

        if (!string.Equals(dataList.NormalizedName, normalizedName, StringComparison.Ordinal))
        {
            var existingDataList = await _lists.NamedAsync(normalizedName, cancellationToken);
            if (existingDataList is not null && existingDataList.Id != dataList.Id)
            {
                return Result.Invalid(CreateDuplicateNameValidationError(nextName));
            }
        }

        dataList.UpdateDetails(nextName, nextDescription, normalizedName);

        return await _writer.SaveAsync(dataList, nextName, cancellationToken);
    }

    internal static ValidationError CreateDuplicateNameValidationError(string name) => new()
    {
        Identifier = nameof(UpdateDataListDetailsCommand.Name),
        ErrorMessage = $"A data list with the name '{name}' already exists.",
        ErrorCode = DuplicateNameErrorCode
    };

    internal sealed class Registered(DataListRows lists, IValueNormalizer valueNormalizer, DataListDetailsWriter writer)
        : RegisteredContentHandler<UpdateDataListDetailsCommand, Result<DataListDto>>(
            new UpdateDataListDetailsHandler(lists, valueNormalizer, writer));
}
