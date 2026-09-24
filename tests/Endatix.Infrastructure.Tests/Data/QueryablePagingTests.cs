using Endatix.Core.Infrastructure.Paging;
using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Tests.Data;

public sealed class QueryablePagingTests
{
    [Fact]
    public async Task ToPagedAsync_MiddlePage_ReturnsOrderedMappedRows()
    {
        // Arrange
        await using var db = await SeedAsync(5);

        // Act
        var paged = await db.Rows.ToPagedAsync(
            new PageRequest(page: 2, pageSize: 2), Ordered, row => row.Name, TestContext.Current.CancellationToken);

        // Assert
        paged.Page.Should().Be(2);
        paged.TotalRecords.Should().Be(5);
        paged.TotalPages.Should().Be(3);
        paged.Items.Should().Equal("row-3", "row-4");
    }

    [Theory]
    [InlineData(4)]
    [InlineData(int.MaxValue)]
    public async Task ToPagedAsync_PagePastTheEnd_ReturnsTheLastPage(int page)
    {
        // Arrange
        await using var db = await SeedAsync(5);

        // Act
        var paged = await db.Rows.ToPagedAsync(
            new PageRequest(page, pageSize: 2), Ordered, row => row.Name, TestContext.Current.CancellationToken);

        // Assert
        paged.Page.Should().Be(3);
        paged.Items.Should().Equal("row-5");
    }

    [Fact]
    public async Task ToPagedAsync_NoRows_ReturnsEmptyFirstPage()
    {
        // Arrange
        await using var db = await SeedAsync(0);

        // Act
        var paged = await db.Rows.ToPagedAsync(
            new PageRequest(page: 3, pageSize: 2), Ordered, row => row.Name, TestContext.Current.CancellationToken);

        // Assert
        paged.Page.Should().Be(1);
        paged.TotalRecords.Should().Be(0);
        paged.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ToPagedAsync_FilteredQuery_CountsOnlyMatchingRows()
    {
        // Arrange
        await using var db = await SeedAsync(5);

        // Act
        var paged = await db.Rows.Where(row => row.Id % 2 == 1).ToPagedAsync(
            new PageRequest(page: 1, pageSize: 10), Ordered, row => row.Id, TestContext.Current.CancellationToken);

        // Assert
        paged.TotalRecords.Should().Be(3);
        paged.Items.Should().Equal(1L, 3L, 5L);
    }

    private static IOrderedQueryable<Row> Ordered(IQueryable<Row> rows) => rows.OrderBy(row => row.Id);

    private static async Task<RowsContext> SeedAsync(int count)
    {
        var db = new RowsContext(new DbContextOptionsBuilder<RowsContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        // Inserted in reverse so the result order comes from orderBy, not from insertion.
        db.Rows.AddRange(Enumerable.Range(1, count).Reverse().Select(i => new Row { Id = i, Name = $"row-{i}" }));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return db;
    }

    private sealed class Row
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class RowsContext(DbContextOptions<RowsContext> options) : DbContext(options)
    {
        public DbSet<Row> Rows => Set<Row>();
    }
}
