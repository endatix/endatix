using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Abstractions;

namespace Endatix.Infrastructure.Repositories
{
    public class FormsRepository : EfRepository<Form>, IFormsRepository
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly AppDbContext _dbContext;

        public FormsRepository(AppDbContext dbContext, IUnitOfWork unitOfWork)
            : base(dbContext)
        {
            _dbContext = dbContext;
            _unitOfWork = unitOfWork;
        }

        public async Task<FormDefinition> AddNewFormDefinitionAsync(Form form, FormDefinition formDefinition, CancellationToken cancellationToken = default) {
            form.AddFormDefinition(formDefinition);
            
            _dbContext.Set<FormDefinition>().Add(formDefinition);
            
            await _dbContext.SaveChangesAsync(cancellationToken);
            
            return formDefinition;
        }

        public async Task<Form> CreateFormWithDefinitionAsync(Form form, FormDefinition formDefinition, CancellationToken cancellationToken = default)
        {
            try
            {
                await _unitOfWork.BeginTransactionAsync(cancellationToken);

                // Save form first
                await AddAsync(form, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                // Add and save form definition
                form.AddFormDefinition(formDefinition);
                _dbContext.Set<FormDefinition>().Add(formDefinition);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                return form;
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }
    }
}