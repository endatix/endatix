namespace Endatix.Modules.Audience.Shared;

/// <summary>
/// Paging limits for the audience directory. Higher than the shared list limits, so up to 5,000 people on a
/// form can be read in one page.
/// </summary>
internal static class AudiencePaging
{
    public const int DefaultPageSize = 50;

    public const int MaxPageSize = 5_000;
}
