using System;
using System.Collections.Generic;

namespace Endatix.Core.Infrastructure.Result;

public interface IResult
{
    ResultStatus Status { get; }
    IEnumerable<string> Errors { get; }
    IEnumerable<ValidationError> ValidationErrors { get; }
    Type ValueType { get; }
    object GetValue();
}
