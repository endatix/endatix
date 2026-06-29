using Endatix.Core.Abstractions.Data;
using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Tests.Data
{
    public class UniqueConstraintViolationCheckerTests
    {
        private readonly UniqueConstraintViolationChecker _sut = new();
        private const string SqlServerConstraintName = "IX_DataLists_TenantId_NormalizedName_Unique";

        [Fact]
        public void AnalyzeUniqueConstraint_WhenNotDbUpdateException_ReturnsNotViolation()
        {
            var result = _sut.AnalyzeUniqueConstraint(new InvalidOperationException("oops"));

            result.IsUniqueConstraintViolation.Should().BeFalse();
            result.ConstraintName.Should().BeNull();
            result.ColumnName.Should().BeNull();
        }

        [Fact]
        public void AnalyzeUniqueConstraint_WhenPostgresUniqueViolation_ReturnsViolationWithNames()
        {
            Exception postgresException = new Npgsql.PostgresException("23505", "IX_DataLists_TenantId_Name", "Name");
            Exception exception = new DbUpdateException("save failed", postgresException);

            var result = _sut.AnalyzeUniqueConstraint(exception);

            result.IsUniqueConstraintViolation.Should().BeTrue();
            result.ConstraintName.Should().Be("IX_DataLists_TenantId_Name");
            result.ColumnName.Should().Be("Name");
        }

        [Fact]
        public void AnalyzeUniqueConstraint_WhenPostgresNonUniqueViolation_ReturnsNotViolation()
        {
            Exception postgresException = new Npgsql.PostgresException("42P01", "IX_Other", null);
            Exception exception = new DbUpdateException("save failed", postgresException);

            var result = _sut.AnalyzeUniqueConstraint(exception);

            result.IsUniqueConstraintViolation.Should().BeFalse();
        }

        [Fact]
        public void AnalyzeUniqueConstraint_WhenSqlServerUniqueViolation_ReturnsViolation()
        {
            Exception sqlException = new Microsoft.Data.SqlClient.SqlException(2601);
            Exception exception = new DbUpdateException("save failed", sqlException);

            var result = _sut.AnalyzeUniqueConstraint(exception);

            result.IsUniqueConstraintViolation.Should().BeTrue();
            result.ConstraintName.Should().BeNull();
            result.ColumnName.Should().BeNull();
        }

        [Fact]
        public void AnalyzeUniqueConstraint_WhenSqlServerUniqueViolationMessageContainsConstraint_ExtractsConstraintName()
        {
            var message = $"Violation of UNIQUE KEY constraint '{SqlServerConstraintName}'. Cannot insert duplicate key.";
            Exception sqlException = new Microsoft.Data.SqlClient.SqlException(2627, message);
            Exception exception = new DbUpdateException("save failed", sqlException);

            var result = _sut.AnalyzeUniqueConstraint(exception);

            result.IsUniqueConstraintViolation.Should().BeTrue();
            result.ConstraintName.Should().Be(SqlServerConstraintName);
            result.ColumnName.Should().BeNull();
        }

        [Fact]
        public void AnalyzeUniqueConstraint_WhenSqlServerNonUniqueViolation_ReturnsNotViolation()
        {
            Exception sqlException = new Microsoft.Data.SqlClient.SqlException(547);
            Exception exception = new DbUpdateException("save failed", sqlException);

            var result = _sut.AnalyzeUniqueConstraint(exception);

            result.IsUniqueConstraintViolation.Should().BeFalse();
        }
    }
}

namespace Npgsql
{
    internal sealed class PostgresException(string sqlState, string constraintName, string? columnName) : Exception("Postgres")
    {
        public string SqlState { get; } = sqlState;

        public string ConstraintName { get; } = constraintName;

        public string? ColumnName { get; } = columnName;
    }
}

namespace Microsoft.Data.SqlClient
{
    internal sealed class SqlException(int number, string? message = null) : Exception(message ?? "SqlServer")
    {
        public int Number { get; } = number;
    }
}
