using Microsoft.Extensions.Options;

namespace Endatix.Modules.Audience;

/// <summary>Fails the host at startup on Audience settings the module cannot run with.</summary>
internal sealed class AudienceOptionsValidator : IValidateOptions<AudienceOptions>
{
    private const string ImportChunkRowsKey = $"{AudienceOptions.SectionName}:{nameof(AudienceOptions.ImportChunkRows)}";

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, AudienceOptions options) =>
        options.ImportChunkRows is >= 1 and <= AudienceOptions.MaxImportChunkRows
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"{ImportChunkRowsKey} must be from 1 to {AudienceOptions.MaxImportChunkRows}; it is {options.ImportChunkRows}.");
}
