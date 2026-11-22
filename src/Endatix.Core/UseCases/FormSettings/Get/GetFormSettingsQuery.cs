using System.Diagnostics.CodeAnalysis;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormSettings.Get;

/// <summary>Reads a form's settings: every section, with defaults where nothing is stored.</summary>
/// <param name="FormId">The form.</param>
[Experimental("ENDATIX0005")]
public sealed record GetFormSettingsQuery(long FormId) : IQuery<Result<FormSettingsDto>>;
