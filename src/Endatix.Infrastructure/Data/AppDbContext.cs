using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Endatix.Core.Entities;
using Endatix.Core.Configuration;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Endatix.Core.Abstractions;

namespace Endatix.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        private readonly ILogger _logger;
        private readonly IIdGenerator _idGenerator;
        public AppDbContext(DbContextOptions<AppDbContext> options, ILogger<AppDbContext> logger, IIdGenerator idGenerator) : base(options)
        {
            _logger = logger;
            _idGenerator = idGenerator;
            // TODO: Make optional
            bool isDatabaseNew = this.Database.EnsureCreated();

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
            // Create a list of assemblies to scan for model builder configurations
            List<Assembly> assemblies = new List<Assembly>();

            // The entry assembly might contain user defined model builder configurations
            Assembly entryAssembly = Assembly.GetEntryAssembly();
            assemblies.Add(entryAssembly);

            // Get all Endatix assemblies that are referenced within the project
            var appDomainAssemblies = AppDomain.CurrentDomain.GetAssemblies().
                Where(assembly => assembly.GetName().Name.StartsWith("Endantix."));

            assemblies.AddRange(appDomainAssemblies);

            // Scan each assembly for model builder configurations and apply them
            foreach (Assembly assembly in assemblies)
            {
                builder.ApplyConfigurationsFromAssembly(assembly);
            }

            #region Prefix table names
            foreach (var entity in builder.Model.GetEntityTypes())
            {
                builder.Entity(entity.Name).ToTable(TableNamePrefix.GetTableName(entity.Name));
            }
            #endregion

            base.OnModelCreating(builder);
        }

        public override int SaveChanges()
        {
            ProcessEntities();
            return base.SaveChanges();
        }

        public async override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            ProcessEntities();
            return await base.SaveChangesAsync(true, cancellationToken);
        }

        private void ProcessEntities()
        {
            var entries = this.ChangeTracker.Entries()
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
    }
}