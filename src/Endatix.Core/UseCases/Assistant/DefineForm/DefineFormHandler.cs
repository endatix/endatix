using System.Threading;
using System.Threading.Tasks;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Assistant.DefineForm;

public class DefineFormHandler : ICommandHandler<DefineFormCommand, Result<string>>
{
    private readonly IAssistantService assistantService;

    public DefineFormHandler(IAssistantService assistantService)
    {
        this.assistantService = assistantService;
    }

    public async Task<Result<string>> Handle(DefineFormCommand request, CancellationToken cancellationToken)
    {
        try
        {
            string formDefinition = assistantService.DefineForm(request.Prompt, request.Definition);
            return Result.Success(formDefinition);
        }
        catch (Exception ex)
        {
            return Result.Error($"Failed to define form: {ex.Message}");
        }
    }
}
