using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Abstractions;

namespace Endatix.Infrastructure.Repositories
{
    public class FormsRepository<TAppDbContext> : EfRepository<Form, TAppDbContext>, IFormsRepository
        where TAppDbContext : AppDbContext
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly TAppDbContext _dbContext;

        public FormsRepository(TAppDbContext dbContext, IUnitOfWork unitOfWork)
            : base(dbContext)
        {
            _dbContext = dbContext;
            _unitOfWork = unitOfWork;
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

    public class FormsRepository : FormsRepository<AppDbContext>
    {
        public FormsRepository(AppDbContext dbContext, IUnitOfWork unitOfWork)
            : base(dbContext, unitOfWork)
        {
        }
    }
}
