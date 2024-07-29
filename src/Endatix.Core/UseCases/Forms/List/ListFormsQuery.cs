using System.Collections.Generic;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Forms.List;

/// <summary>
/// Query for listing forms with pagination.
/// </summary>
public record ListFormsQuery(int? Page, int? PageSize) : IQuery<Result<IEnumerable<Form>>>;
