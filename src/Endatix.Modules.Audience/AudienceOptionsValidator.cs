using Microsoft.Extensions.Options;

namespace Endatix.Modules.Audience;

/// <summary>Fails the host at startup on Audience settings the module cannot run with.</summary>
internal sealed class AudienceOptionsValidator : IValidateOptions<AudienceOptions>
{
    private const string IMPORT_CHUNK_ROWS_KEY = $"{AudienceOptions.SECTION_NAME}:{nameof(AudienceOptions.ImportChunkRows)}";

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, AudienceOptions options) =>
        options.ImportChunkRows is >= 1 and <= AudienceOptions.MAX_IMPORT_CHUNK_ROWS
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"{IMPORT_CHUNK_ROWS_KEY} must be from 1 to {AudienceOptions.MAX_IMPORT_CHUNK_ROWS}; it is {options.ImportChunkRows}.");
}
