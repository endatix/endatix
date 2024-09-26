using Microsoft.EntityFrameworkCore;
using Endatix.Core.Entities;
using Endatix.Core.Configuration;
using Microsoft.Extensions.Logging;
using Endatix.Core.Abstractions;
using Endatix.Framework;

namespace Endatix.Infrastructure.Data
{
    /// <summary>
    /// Represents the application database context for persisting the Endatix Domain entities
    /// </summary>
    public class AppDbContext : DbContext
    {
        private readonly ILogger _logger;
        private readonly IIdGenerator _idGenerator;
        public AppDbContext(DbContextOptions<AppDbContext> options, ILogger<AppDbContext> logger, IIdGenerator idGenerator) : base(options)
        {
            _logger = logger;
            _idGenerator = idGenerator;
            // TODO: Make optional
            var isDatabaseNew = this.Database.EnsureCreated();

            if (isDatabaseNew && EndatixConfig.Configuration.SeedSampleData)
            {
                var dataSeeder = new DataSeeder(_logger, _idGenerator, this);
                dataSeeder.PopulateTestData();
            }
        }

        public DbSet<Form> Forms { get; set; }

        public DbSet<Submission> Submissions { get; set; }

        public DbSet<FormDefinition> FormDefinitions { get; set; }

        public override void AddRange(IEnumerable<object> entities)
        {
            base.AddRange(entities);
        }

        public override void AddRange(params object[] entities)
        {
            base.AddRange(entities);
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            var endatixAssemblies = GetType().Assembly.GetEndatixPlatormAssemblies();
            foreach (var assembly in endatixAssemblies)
            {
                builder.ApplyConfigurationsFromAssembly(assembly);
            }

            PrefixTableNames(builder);

            base.OnModelCreating(builder);
        }

        public override int SaveChanges()
        {
            ProcessEntities();
            return base.SaveChanges();
        }

        /// <inheritdoc/>
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ProcessEntities();
            return await base.SaveChangesAsync(true, cancellationToken);
        }

        private void ProcessEntities()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        // Generate an id if necessary
                        if (entry.CurrentValues["Id"] is default(long))
                        {
                            entry.CurrentValues["Id"] = _idGenerator.CreateId();
                        }
                        // Set the CreatedAt value
                        entry.CurrentValues["CreatedAt"] = DateTime.UtcNow;
                        break;
                    case EntityState.Modified:
                        // Set the ModifiedAt value
                        entry.CurrentValues["ModifiedAt"] = DateTime.UtcNow;
                        break;
                }
            }
        }

        private void PrefixTableNames(ModelBuilder builder)
        {
            if (builder?.Model?.GetEntityTypes() == null)
            {
                return;
            }

            foreach (var entity in builder.Model.GetEntityTypes())
            {
                builder.Entity(entity.Name).ToTable(TableNamePrefix.GetTableName(entity.Name));
            }
        }
    }
}