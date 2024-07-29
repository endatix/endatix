using System.Collections.Generic;
using System.Threading.Tasks;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using NotFoundException = Endatix.Core.Exceptions.NotFoundException;

namespace Endatix.Core.Services;

public class FormService : IFormService
{
    private readonly IRepository<Form> _formRepository;
    private readonly IRepository<FormDefinition> _formDefinitionRepository;
    private readonly IRepository<Submission> _submissionRepository;

    public FormService(IRepository<Form> formRepository, IRepository<FormDefinition> formDefinitionRepository, IRepository<Submission> submissionRepository)
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
        throw new System.NotImplementedException();
    }

    public async Task<Submission> AddSubmissionAsync(long formId, Submission submission)
    {
        var formDefinition = await GetActiveFormDefinitionAsync(formId);
        if (formDefinition == null)
        {
            throw new NotFoundException($"Definition for form with ID {formId} was not found.");
        }

        submission.FormDefinitionId = formDefinition.Id;

        await _submissionRepository.AddAsync(submission);
        return submission;
    }

    public async Task<Submission> AddSubmissionAsync(Form form, Submission submission)
    {
        return await AddSubmissionAsync(form.Id, submission);
    }

    public Task UpdateSubmissionAsync(long formId, Submission submission)
    {
        throw new System.NotImplementedException();
    }

    public Task UpdateSubmissionAsync(Form form, Submission submission)
    {
        throw new System.NotImplementedException();
    }

    public Task SetActiveFormDefinitionAsync(Form form, FormDefinition formDefinition)
    {
        throw new System.NotImplementedException(); // TBD and implemented when versioning is fully supported, old implementation is in Git
    }

    public async Task<FormDefinition> GetActiveFormDefinitionAsync(long formId)
    {
        var spec = new ActiveFormDefinitionByFormIdSpec(formId);
        var formDefinition = await _formDefinitionRepository.SingleOrDefaultAsync(spec);
        return formDefinition;
    }

    public async Task UpdateFormDefinitionAsync(long formDefinitionId, string jsonData)
    {
        var formDefinition = await _formDefinitionRepository.GetByIdAsync(formDefinitionId);
        if (formDefinition == null)
        {
            throw new Exceptions.NotFoundException(formDefinitionId);
        }

        formDefinition.JsonData = jsonData;
        await _formDefinitionRepository.UpdateAsync(formDefinition);
    }

    public async Task UpdateActiveFormDefinitionAsync(long formId, string jsonData)
    {
        var spec = new ActiveFormDefinitionByFormIdSpec(formId);
        var formDefinition = await _formDefinitionRepository.SingleOrDefaultAsync(spec);
        if (formDefinition == null)
        {
            throw new NotFoundException($"Definition for form with ID {formId} was not found.");
        }

        formDefinition.JsonData = jsonData;
        await _formDefinitionRepository.UpdateAsync(formDefinition);
    }
}
