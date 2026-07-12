using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Features.FormSchema.Export;

/// <summary>
/// Generates Shoji/Crunch codebook JSON from format-neutral <see cref="Domain.FormSchema.Codebook"/> + flattening map.
/// Implemented in E6 export work.
/// </summary>
internal static class ShojiCodebookGenerator
{
    internal static string Generate(MergedFormSchema flatteningMap, string codebookJson) =>
        throw new NotImplementedException("Shoji codebook generation is implemented in E6 export work.");
}
