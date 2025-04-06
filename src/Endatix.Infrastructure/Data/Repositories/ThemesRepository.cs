using Ardalis.GuardClauses;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Exceptions = Endatix.Core.Exceptions;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Repositories;

public sealed class ThemesRepository : EfRepository<Theme>, IThemesRepository
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly AppDbContext _dbContext;

    public ThemesRepository(AppDbContext dbContext, IUnitOfWork unitOfWork)
        : base(dbContext)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<Form>> GetFormsByThemeIdAsync(long themeId, CancellationToken cancellationToken = default)
    {
        Guard.Against.NegativeOrZero(themeId, nameof(themeId));

        return await _dbContext.Forms
            .Where(f => f.ThemeId == themeId)
            .ToListAsync(cancellationToken);
    }

    public async Task<Form> AssignThemeToFormAsync(long formId, long themeId, CancellationToken cancellationToken = default)
    {
        Guard.Against.NegativeOrZero(formId, nameof(formId));
        Guard.Against.NegativeOrZero(themeId, nameof(themeId));

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var form = await _dbContext.Forms.FindAsync(new object[] { formId }, cancellationToken) 
                ?? throw new Exceptions.NotFoundException(formId);

            var theme = await _dbContext.Themes.FindAsync(new object[] { themeId }, cancellationToken)
                ?? throw new Exceptions.NotFoundException(themeId);

            // Ensure form and theme belong to the same tenant
            if (form.TenantId != theme.TenantId)
            {
                throw new InvalidOperationException("Form and theme must belong to the same tenant");
            }

            form.SetTheme(theme);
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

    public async Task<Form> RemoveThemeFromFormAsync(long formId, CancellationToken cancellationToken = default)
    {
        Guard.Against.NegativeOrZero(formId, nameof(formId));

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var form = await _dbContext.Forms.FindAsync(new object[] { formId }, cancellationToken) 
                ?? throw new Exceptions.NotFoundException(formId);

            form.SetTheme(null);
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