using System;

namespace Endatix.Core.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(long entityId)
        : base($"Entity with ID {entityId} was not found.")
    {
    }

    public NotFoundException(string message)
        : base(message)
    {
    }
}