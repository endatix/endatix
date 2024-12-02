using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using NotFoundException = Endatix.Core.Exceptions.NotFoundException;

namespace Endatix.Core.Services;

public class FormService : IFormService
{
    private readonly IFormsRepository _formRepository;
    private readonly IRepository<FormDefinition> _formDefinitionRepository;
    private readonly IRepository<Submission> _submissionRepository;

    public FormService(IFormsRepository formRepository, IRepository<FormDefinition> formDefinitionRepository, IRepository<Submission> submissionRepository)
    {
        _formRepository = formRepository;
        _formDefinitionRepository = formDefinitionRepository;
        _submissionRepository = submissionRepository;
    }

    public async Task<Form> CreateFormAsync(string name, string formDefinitionJson = null, string description = null, bool isEnabled = false)
    {
        throw new System.NotImplementedException();
    }

    public async Task DeleteFormAsync(long formId)
    {
        var form = await _formRepository.GetByIdAsync(formId);
        await _formRepository.DeleteAsync(form);
    }
    public async Task<IEnumerable<Form>> GetFormsAsync()
    {
        return await _formRepository.ListAsync();
    }

    public async Task<List<Submission>> GetSubmissionsAsync(long formId)
    {
        throw new NotImplementedException();
    }

    public Task UpdateSubmissionAsync(long formId, Submission submission)
    {
        throw new NotImplementedException();
    }

    public Task UpdateSubmissionAsync(Form form, Submission submission)
    {
        throw new NotImplementedException();
    }

    public Task SetActiveFormDefinitionAsync(Form form, FormDefinition formDefinition)
    {
        throw new NotImplementedException(); // TBD and implemented when versioning is fully supported, old implementation is in Git
    }

    public async Task<FormDefinition> GetActiveFormDefinitionAsync(long formId)
    {
        var spec = new ActiveFormDefinitionByFormIdSpec(formId);
        var formWithActiveDefinition = await _formRepository.SingleOrDefaultAsync(spec);
        var activeDefinition = formWithActiveDefinition?.ActiveDefinition;
        return activeDefinition;
    }

    public async Task UpdateFormDefinitionAsync(long formDefinitionId, string jsonData)
    {
        var formDefinition = await _formDefinitionRepository.GetByIdAsync(formDefinitionId);
        if (formDefinition == null)
        {
            throw new NotFoundException(formDefinitionId);
        }

        formDefinition.UpdateSchema(jsonData);
        await _formDefinitionRepository.UpdateAsync(formDefinition);
    }

    public async Task UpdateActiveFormDefinitionAsync(long formId, string jsonData)
    {
        var spec = new ActiveFormDefinitionByFormIdSpec(formId);
        var formWithActiveDefinition = await _formRepository.SingleOrDefaultAsync(spec);
        var activeDefinition = formWithActiveDefinition?.ActiveDefinition;
        if (formWithActiveDefinition == null || activeDefinition == null)
        {
            throw new NotFoundException($"Definition for form with ID {formId} was not found.");
        }

        activeDefinition.UpdateSchema(jsonData);
        await _formRepository.UpdateAsync(formWithActiveDefinition);
    }
}
