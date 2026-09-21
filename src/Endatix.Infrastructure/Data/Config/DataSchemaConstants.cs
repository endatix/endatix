namespace Endatix.Infrastructure.Data.Config;

/// <summary>
/// EF/API re-export of <see cref="Endatix.Core.Common.DataSchemaConstants"/>.
/// Prefer Core for domain; this type keeps existing Infrastructure/Api usings working.
/// </summary>
public static class DataSchemaConstants
{
    public const int MIN_NAME_LENGTH = Endatix.Core.Common.DataSchemaConstants.MIN_NAME_LENGTH;
    public const int MAX_NAME_LENGTH = Endatix.Core.Common.DataSchemaConstants.MAX_NAME_LENGTH;
    public const int MIN_JSON_LENGTH = Endatix.Core.Common.DataSchemaConstants.MIN_JSON_LENGTH;
    public const int MAX_DESCRIPTION_LENGTH = Endatix.Core.Common.DataSchemaConstants.MAX_DESCRIPTION_LENGTH;
    public const int MAX_SLUG_LENGTH = Endatix.Core.Common.DataSchemaConstants.MAX_SLUG_LENGTH;
    public const int MAX_ERROR_LENGTH = Endatix.Core.Common.DataSchemaConstants.MAX_ERROR_LENGTH;
    public const int MAX_EMAIL_LENGTH = Endatix.Core.Common.DataSchemaConstants.MAX_EMAIL_LENGTH;
}
