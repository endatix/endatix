using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Data;
using Endatix.Infrastructure.Data.Querying;
using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.Links;
using Endatix.Modules.Audience.Features.People;
using Endatix.Modules.Audience.Persistence;
using Endatix.Modules.Audience.Persistence.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.Modules.Audience.Tests.Features.Links;

public sealed class LinkBatchIssueTests
{
    private static readonly IValueNormalizer EmailNormalizer = new UpperInvariantNormalizer();

    [Fact]
    public async Task IssueAsync_AfterSave_DoesNotQueryDatabase()
    {
        // Arrange
        const long tenantId = 1;
        const long formId = 10;
        await using var inner = CreateContext(tenantId);
        var member = new Member(new MemberCreateArgs(
            tenantId, "ada@example.com", AudienceIdentifierKindCodes.Email, EmailNormalizer))
        { Id = 101 };
        var membership = new Membership(new MembershipCreateArgs(tenantId, formId, member.Id, IsTest: false))
        {
            Id = 201,
        };
        inner.Members.Add(member);
        inner.Memberships.Add(membership);
        await inner.SaveChangesAsync(TestContext.Current.CancellationToken);

        var guard = new QueryAfterSaveGuard(inner);
        var batch = new LinkBatch(
            guard,
            new PeopleQuery(guard, Substitute.For<IRelationalSubstringLikeFilter>()),
            Substitute.For<IUniqueConstraintViolationChecker>(),
            NullLogger<LinkBatch>.Instance);
        var scope = new LinkBatchScope(tenantId, formId, new PeopleFilter());

        // Act
        var result = await batch.IssueAsync(scope, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Issued.Should().ContainSingle()
            .Which.Token.Should().NotBeNullOrEmpty();
    }

    private static LinkBatchInMemoryContext CreateContext(long tenantId)
    {
        var tenant = Substitute.For<ITenantContext>();
        tenant.TenantId.Returns(tenantId);
        var options = new DbContextOptionsBuilder<LinkBatchInMemoryContext>()
            .UseInMemoryDatabase($"link-batch-{Guid.NewGuid()}")
            .Options;
        return new LinkBatchInMemoryContext(options, tenant);
    }

    /// <summary>In-memory audience context for link-batch unit tests.</summary>
    private sealed class LinkBatchInMemoryContext(
        DbContextOptions<LinkBatchInMemoryContext> options,
        ITenantContext tenantContext)
        : AudienceDbContextBase(options, tenantContext)
    {
        protected override void EnsureSupportedProvider()
        {
        }

        private protected override AudienceSqlDialect SqlDialect => AudienceSqlDialect.SqlServer;
    }

    /// <summary>Throws when any DbSet is read after <see cref="IAudienceDbContext.SaveChangesAsync"/>.</summary>
    private sealed class QueryAfterSaveGuard(LinkBatchInMemoryContext inner) : IAudienceDbContext
    {
        private bool _saved;

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var count = await inner.SaveChangesAsync(cancellationToken);
            _saved = true;
            return count;
        }

        public long GetTenantId() => inner.GetTenantId();

        public DbSet<AudienceSettings> AudienceSettings => Read(inner.AudienceSettings);

        public DbSet<Property> Properties => Read(inner.Properties);

        public DbSet<Member> Members => Read(inner.Members);

        public DbSet<Membership> Memberships => Read(inner.Memberships);

        public DbSet<PropertyValue> PropertyValues => Read(inner.PropertyValues);

        public DbSet<AudienceImport> AudienceImports => Read(inner.AudienceImports);

        public DbSet<AudienceImportChunk> ImportChunks => Read(inner.ImportChunks);

        public DbSet<AudienceImportRejection> ImportRejections => Read(inner.ImportRejections);

        public DbSet<AudienceLink> Links => Read(inner.Links);

        private DbSet<T> Read<T>(DbSet<T> set) where T : class
        {
            if (_saved)
            {
                throw new InvalidOperationException("Database read after SaveChangesAsync.");
            }

            return set;
        }
    }

    private sealed class UpperInvariantNormalizer : IValueNormalizer
    {
        public string? Normalize(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    }
}
