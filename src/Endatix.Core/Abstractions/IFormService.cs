using System.Collections.Generic;
using System.Threading.Tasks;
using Endatix.Core.Entities;

namespace Endatix.Core.Abstractions;

  public interface IFormService
  {
      Task<IEnumerable<Form>> GetFormsAsync();
      Task<Form> CreateFormAsync(string name, string formDefinitionJson = null, string description = null, bool isEnabled = false);
      Task DeleteFormAsync(long formId);
      Task<List<Submission>> GetSubmissionsAsync(long formDefinitionId);
      Task<Submission> AddSubmissionAsync(long formId, Submission submission);
      Task<Submission> AddSubmissionAsync(Form form, Submission submission);
      Task UpdateSubmissionAsync(long formId, Submission submission);
      Task UpdateSubmissionAsync(Form form, Submission submission);
      Task SetActiveFormDefinitionAsync(Form form, FormDefinition formDefinition);
      Task<FormDefinition> GetActiveFormDefinitionAsync(long formId);
      Task UpdateFormDefinitionAsync(long formDefinitionId, string jsonData);
      Task UpdateActiveFormDefinitionAsync(long formId, string jsonData);
  }