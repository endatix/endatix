using System.Text.Json;
using System.Text.Json.Serialization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;

namespace Endatix.Modules.Reporting.Features.Export;

/// <summary>
/// Validates and serializes export format settings for persistence.
/// </summary>
internal sealed class ExportFormatSettingsWriter
{
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new ColumnAliasProfileJsonConverter() },
    };

    internal Result<string?> Serialize(ExportFormatSettingsInput? settings) =>
        Serialize(settings, ExportTarget.Submissions);

    internal Result<string?> Serialize(
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

        var persisted = NormalizeForPersistence(exportSettings, exportTarget);
        PersistableExportFormatSettings payload = new(
            persisted.AliasProfile,
            persisted.KeySeparator,
            exportTarget == ExportTarget.Submissions ? persisted.IncludeTestSubmissions : null);
        return Result.Success<string?>(JsonSerializer.Serialize(payload, _serializerOptions));
    }

    private sealed record PersistableExportFormatSettings(
        ColumnAliasProfile AliasProfile,
        string KeySeparator,
        bool? IncludeTestSubmissions);

    internal static Result Validate(ExportFormatSettings settings) =>
        Validate(settings, ExportTarget.Submissions);

    internal static Result Validate(
        ExportFormatSettings settings,
        ExportTarget exportTarget)
    {
        List<ValidationError> errors = [];

        try
        {
            ExportFormatSettings.RequireKeySeparator(settings.KeySeparator);
        }
        catch (ArgumentException ex)
        {
            errors.Add(new ValidationError
            {
                Identifier = nameof(ExportFormatSettings.KeySeparator),
                ErrorMessage = ex.Message,
            });
        }

        if (!Enum.IsDefined(settings.AliasProfile))
        {
            errors.Add(new ValidationError
            {
                Identifier = nameof(ExportFormatSettings.AliasProfile),
                ErrorMessage = "Column naming profile is invalid.",
            });
        }

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

    /// <summary>
    /// Locale is request-time only; do not persist it on format definitions.
    /// Column scope is request-time only.
    /// </summary>
    private static ExportFormatSettings NormalizeForPersistence(
        ExportFormatSettings settings,
        ExportTarget exportTarget)
    {
        if (exportTarget == ExportTarget.Codebook)
        {
            return new ExportFormatSettings(
                AliasProfile: settings.AliasProfile,
                Locale: ExportFormatSettings.Default.Locale,
                ColumnScope: null,
                IncludeTestSubmissions: false,
                KeySeparator: settings.KeySeparator);
        }

        return new ExportFormatSettings(
            AliasProfile: settings.AliasProfile,
            Locale: ExportFormatSettings.Default.Locale,
            ColumnScope: null,
            IncludeTestSubmissions: settings.IncludeTestSubmissions,
            KeySeparator: settings.KeySeparator);
    }
}
