using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Abstractions.Repositories;

/// <summary>
/// Defines the contract for a repository that handles forms and their definitions, extending Ardalis.ISpecification with custom logic.
/// </summary>
public interface IFormsRepository : IRepository<Form>
{
    /// <summary>
    /// Creates a new form with its definition asynchronously.
    /// </summary>
    /// <param name="form">The form to be created.</param>
    /// <param name="formDefinition">The definition of the form.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<Form> CreateFormWithDefinitionAsync(Form form, FormDefinition formDefinition, CancellationToken cancellationToken = default);
    Task<FormDefinition> AddNewFormDefinitionAsync(Form form, FormDefinition formDefinition, CancellationToken cancellationToken = default);
}