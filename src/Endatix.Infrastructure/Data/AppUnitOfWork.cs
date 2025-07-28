namespace Endatix.Infrastructure.Data
{
    /// <summary>
    /// Implements the Unit of Work pattern using Entity Framework Core for the main AppDbContext.
    /// </summary>
    public class AppUnitOfWork : EfUnitOfWorkBase<AppDbContext>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AppUnitOfWork"/> class.
        /// </summary>
        /// <param name="context">The Entity Framework Core DbContext.</param>
        public AppUnitOfWork(AppDbContext context) : base(context)
        {
        }
    }
}