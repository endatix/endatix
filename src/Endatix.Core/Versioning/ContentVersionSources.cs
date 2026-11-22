namespace Endatix.Core.Versioning;

/// <summary>How a <see cref="IContentVersion"/> was made. Stored values: they must not change.</summary>
internal static class ContentVersionSources
{
    /// <summary>The longest source value a column must hold, with room for later sources such as an agent publish.</summary>
    public const int MAX_LENGTH = 32;

    /// <summary>New content was published.</summary>
    public const string PUBLISH = "publish";

    /// <summary>An earlier version was published again, as a copy.</summary>
    public const string RESTORE = "restore";
}
