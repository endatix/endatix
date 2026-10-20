using Endatix.Core.Abstractions.Data;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;

namespace Endatix.Infrastructure.Repositories
{
    public class FormsRepository : EfRepository<Form>, IFormsRepository
    {
        /// <summary>
        /// Rows per statement. Each soft-deleted row also takes key locks in the indexes on the columns the statement
        /// sets, so a batch stays well under the 5,000 locks of SQL Server's escalation.
        /// </summary>
        internal const int SubmissionDeleteBatch = 1000;

        private readonly IUnitOfWork _unitOfWork;
        private readonly AppDbContext _dbContext;

        public FormsRepository(AppDbContext dbContext, IUnitOfWork unitOfWork, EndatixSpecificationEvaluator specificationEvaluator)
            : base(dbContext, specificationEvaluator)
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
                // Raise form.created now — after the active definition is set, before this save — so the outbox
                // captures it atomically in THIS transaction with a populated activeDefinitionId.
                form.RaiseCreated();
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

        /// <inheritdoc/>
        /// <remarks>
        /// In batches of <see cref="SubmissionDeleteBatch"/>, all in the caller's transaction. A batch locks at most that
        /// many rows, with their index entries, so on SQL Server no statement reaches the 5,000 locks at which it
        /// escalates to a lock on the whole Submissions table, which would hold up every tenant until the deletion
        /// commits. The row locks the batches keep until then are on this form's submissions only, which no other
        /// writer may change once the form is being deleted.
        /// </remarks>
        public async Task<int> DeleteSubmissionsAsync(Form form, CancellationToken cancellationToken)
        {
            try
            {
                return await _dbContext.Submissions
                    .Where(submission => submission.TenantId == form.TenantId && submission.FormId == form.Id)
                    .SoftDeleteInBatchesAsync(SubmissionDeleteBatch, cancellationToken);
            }
            catch (Exception exception) when (DatabaseDeadlocks.IsVictim(exception))
            {
                throw DatabaseDeadlocks.Victim(exception);
            }
        }
    }
}