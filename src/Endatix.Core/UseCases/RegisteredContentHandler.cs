using MediatR;

namespace Endatix.Core.UseCases;

/// <summary>Runs the same operation as a direct caller, with its tenant-bound reads chosen by the host.</summary>
internal abstract class RegisteredContentHandler<TRequest, TResult>(IRequestHandler<TRequest, TResult> operation)
    : IRequestHandler<TRequest, TResult> where TRequest : IRequest<TResult>
{
    public Task<TResult> Handle(TRequest request, CancellationToken cancellationToken) =>
        operation.Handle(request, cancellationToken);
}
