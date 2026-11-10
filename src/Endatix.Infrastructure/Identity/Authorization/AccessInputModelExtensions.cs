using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Marks the entity types whose rows decide a user's authorization data. <see cref="AppIdentityDbContext"/> declares
/// them once in its model; a save that changes one moves an <see cref="AccessVersion"/>, and an architecture test
/// forbids bulk writes over them.
/// </summary>
internal static class AccessInputModelExtensions
{
    private const string ACCESS_INPUT_ANNOTATION = "Endatix:AccessInput";

    /// <summary>Declares the entity type an access input.</summary>
    public static EntityTypeBuilder IsAccessInput(this EntityTypeBuilder builder) =>
        builder.HasAnnotation(ACCESS_INPUT_ANNOTATION, true);

    /// <summary>True when the model declares the entity type an access input.</summary>
    public static bool IsAccessInput(this IReadOnlyEntityType entityType) =>
        entityType.FindAnnotation(ACCESS_INPUT_ANNOTATION)?.Value is true;
}
