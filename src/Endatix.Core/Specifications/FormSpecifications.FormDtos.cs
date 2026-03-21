namespace Endatix.Core.Specifications;

/// <summary>
/// DTOs for the Form entity
/// </summary>
public static class FormDtos
{
    /// <summary>
    /// DTO for the IsPublic property of the Form entity
    /// </summary>
    public sealed record IsPublicDto(bool IsPublic, long Id);
}