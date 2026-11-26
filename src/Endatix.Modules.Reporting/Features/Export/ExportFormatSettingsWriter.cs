using System.Text.Json;
using System.Text.Json.Serialization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;

namespace Endatix.Modules.Reporting.Features.Export;

/// <summary>
/// Validates and serializes export format settings for persistence.
/// </summary>
internal static class ExportFormatSettingsWriter
{
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new ColumnAliasProfileJsonConverter() },
    };

    internal static Result<string?> Serialize(ExportFormatSettingsInput? settings) =>
        Serialize(settings, ExportTarget.Submissions);

    internal static Result<string?> Serialize(
        ExportFormatSettingsInput? settings,
        ExportTarget exportTarget)
    {
        if (settings is null)
        {
            return Result.Success<string?>(null);
        }

        var exportSettings = settings.ToSettings();
        var validationResult = Validate(exportSettings, exportTarget);
        if (!validationResult.IsSuccess)
        {
            return Result<string?>.Invalid(validationResult.ValidationErrors);
        }

        return Persist(NormalizeForPersistence(exportSettings, exportTarget), exportTarget);
    }

    private static Result<string?> Persist(ExportFormatSettings persisted, ExportTarget exportTarget)
    {
        PersistableExportFormatSettings payload = new(
            persisted.AliasProfile,
            persisted.KeySeparator,
            exportTarget == ExportTarget.Submissions ? persisted.IncludeTestSubmissions : null,
            persisted.SpecialChoiceCodes);
        return Result.Success<string?>(JsonSerializer.Serialize(payload, _serializerOptions));
    }

    private sealed record PersistableExportFormatSettings(
        ColumnAliasProfile AliasProfile,
        string KeySeparator,
        bool? IncludeTestSubmissions,
        SpecialChoiceCodes? SpecialChoiceCodes);

    internal static Result Validate(ExportFormatSettings settings) =>
        Validate(settings, ExportTarget.Submissions);

    internal static Result Validate(
        ExportFormatSettings settings,
        ExportTarget exportTarget)
    {
        List<ValidationError> errors = [];

        if (new ChoiceCodes(settings.SpecialChoiceCodes).SettingsError() is { } codesError)
        {
            errors.Add(codesError);
        }

        ValidateNaming(settings, errors);
        if (exportTarget == ExportTarget.Codebook &&
            (settings.ColumnScope is { Count: > 0 } || settings.IncludeTestSubmissions))
        {
            errors.Add(new ValidationError
            {
                Identifier = nameof(ExportFormatSettings.IncludeTestSubmissions),
                ErrorMessage = "Codebook exports do not support includeTestSubmissions or columnScope.",
            });
        }

        return errors.Count > 0 ? Result.Invalid(errors) : Result.Success();
    }

    private static void ValidateNaming(ExportFormatSettings settings, List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(settings.KeySeparator))
        {
            errors.Add(new ValidationError("Key separator is invalid.") { Identifier = nameof(ExportFormatSettings.KeySeparator) });
        }

        if (!Enum.IsDefined(settings.AliasProfile))
        {
            errors.Add(new ValidationError("Column naming profile is invalid.") { Identifier = nameof(ExportFormatSettings.AliasProfile) });
        }
    }

    /// <summary>
    /// Locale is request-time only; do not persist it on format definitions.
    /// Column scope is request-time only.
    /// </summary>
    private static ExportFormatSettings NormalizeForPersistence(
        ExportFormatSettings settings,
        ExportTarget exportTarget) =>
        settings with
        {
            Locale = ExportFormatSettings.Default.Locale,
            ColumnScope = null,
            IncludeTestSubmissions = exportTarget == ExportTarget.Submissions && settings.IncludeTestSubmissions,
        };
}
