namespace Endatix.Modules.Audience.Features.Imports;

/// <summary>
/// How many rows each chunk of a staged import holds. An import keeps the size it started with, and clients send
/// chunks of the size its view reports.
/// </summary>
public sealed class ImportChunkingOptions
{
    /// <summary>The configuration section these options bind to.</summary>
    public const string SectionName = "Endatix:Audience:Imports";

    /// <summary>The <see cref="ChunkRows"/> where the host sets none.</summary>
    public const int DefaultChunkRows = 1_000;

    /// <summary>Rows per chunk of a new import; at least 1.</summary>
    public int ChunkRows { get; set; } = DefaultChunkRows;
}
