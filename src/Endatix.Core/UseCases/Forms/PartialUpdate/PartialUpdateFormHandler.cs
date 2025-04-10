using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Forms.PartialUpdate;

public class PartialUpdateFormHandler : ICommandHandler<PartialUpdateFormCommand, Result<Form>>
{
    private readonly IRepository<Form> _repository;
    private readonly IRepository<Theme> _themeRepository;

    public PartialUpdateFormHandler(IRepository<Form> repository, IRepository<Theme> themeRepository)
    {
        _repository = repository;
        _themeRepository = themeRepository;
    }

    public async Task<Result<Form>> Handle(PartialUpdateFormCommand request, CancellationToken cancellationToken)
    {
        var form = await _repository.GetByIdAsync(request.FormId, cancellationToken);
        if (form == null)
        {
            return Result.NotFound("Form not found.");
        }

        form.Name = request.Name ?? form.Name;
        form.Description = request.Description ?? form.Description;
        form.IsEnabled = request.IsEnabled ?? form.IsEnabled;

        if (request.ThemeId.HasValue && form.ThemeId != request.ThemeId)
        {
            var theme = await _themeRepository.GetByIdAsync(request.ThemeId.Value, cancellationToken);
            if (theme == null)
            {
                return Result.NotFound("Form Theme not found.");
            }
            form.SetTheme(theme);
        }
        await _repository.UpdateAsync(form, cancellationToken);
        return Result.Success(form);
    }
}
