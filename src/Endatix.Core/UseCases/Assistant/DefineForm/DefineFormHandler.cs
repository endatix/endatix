using System.Threading;
using System.Threading.Tasks;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Assistant.DefineForm;

public class DefineFormHandler : ICommandHandler<DefineFormCommand, Result<AssistedDefinitionDto>>
{
    private readonly IAssistantService assistantService;

    public DefineFormHandler(IAssistantService assistantService)
    {
        this.assistantService = assistantService;
    }

    public async Task<Result<AssistedDefinitionDto>> Handle(DefineFormCommand request, CancellationToken cancellationToken)
    {
        var assistedDefinition = await assistantService.DefineFormAsync(request.Prompt, request.Definition, request.AssistantId, request.ThreadId);
        return Result.Success(assistedDefinition);
    }
}
