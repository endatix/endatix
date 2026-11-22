using System.Diagnostics.CodeAnalysis;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormSettings.PartialUpdate;

/// <summary>Replaces the sections of a form's settings that <paramref name="Change"/> names.</summary>
/// <param name="FormId">The form.</param>
/// <param name="ExpectedVersion">The version the caller read.</param>
/// <param name="Change">The sections to replace, each whole.</param>
[Experimental("ENDATIX0005")]
public sealed record PartialUpdateFormSettingsCommand(long FormId, long ExpectedVersion, FormSettingsChange Change)
    : ICommand<Result<FormSettingsDto>>;
