namespace Endatix.Modules.Audience;

/// <summary>The Audience module's settings, under <see cref="SectionName"/>.</summary>
public sealed class AudienceOptions
{
    /// <summary>The configuration section these options bind to.</summary>
    public const string SectionName = "Endatix:Audience";

    /// <summary>The largest <see cref="ImportChunkRows"/>: the Hub checks a file's size in chunks of this many rows.</summary>
    public const int MaxImportChunkRows = 1_000;

    /// <summary>
    /// Rows per chunk of a new staged import, from 1 to <see cref="MaxImportChunkRows"/>. An import keeps the size it
    /// started with, and clients send chunks of the size its view reports.
    /// </summary>
    public int ImportChunkRows { get; set; } = MaxImportChunkRows;
}
