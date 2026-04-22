using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Tests.Data
{
    public class UniqueConstraintViolationCheckerTests
    {
        private readonly UniqueConstraintViolationChecker _sut = new();

        [Fact]
        public void IsUniqueConstraintViolation_WhenNotDbUpdateException_ReturnsFalse()
        {
            var result = _sut.IsUniqueConstraintViolation(new InvalidOperationException("oops"));

            result.Should().BeFalse();
        }

        [Fact]
        public void IsUniqueConstraintViolation_WhenPostgresUniqueViolation_ReturnsTrue()
        {
            Exception postgresException = new Npgsql.PostgresException("23505", "IX_DataLists_TenantId_Name");
            Exception exception = new DbUpdateException("save failed", postgresException);

            var result = _sut.IsUniqueConstraintViolation(exception);

            result.Should().BeTrue();
        }

        [Fact]
        public void IsUniqueConstraintViolation_WhenPostgresNonUniqueViolation_ReturnsFalse()
        {
            Exception postgresException = new Npgsql.PostgresException("42P01", "IX_Other");
            Exception exception = new DbUpdateException("save failed", postgresException);

            var result = _sut.IsUniqueConstraintViolation(exception);

            result.Should().BeFalse();
        }

        [Fact]
        public void IsUniqueConstraintViolation_WhenSqlServerUniqueViolation_ReturnsTrue()
        {
            Exception sqlException = new Microsoft.Data.SqlClient.SqlException(2601);
            Exception exception = new DbUpdateException("save failed", sqlException);

            var result = _sut.IsUniqueConstraintViolation(exception);

            result.Should().BeTrue();
        }

        [Fact]
        public void IsUniqueConstraintViolation_WhenSqlServerNonUniqueViolation_ReturnsFalse()
        {
            Exception sqlException = new Microsoft.Data.SqlClient.SqlException(547);
            Exception exception = new DbUpdateException("save failed", sqlException);

            var result = _sut.IsUniqueConstraintViolation(exception);

            result.Should().BeFalse();
        }
    }
}

namespace Npgsql
{
    internal sealed class PostgresException(string sqlState, string constraintName) : Exception("Postgres")
    {
        public string SqlState { get; } = sqlState;
        public string ConstraintName { get; } = constraintName;
    }
}

namespace Microsoft.Data.SqlClient
{
    internal sealed class SqlException(int number) : Exception("SqlServer")
    {
        public int Number { get; } = number;
    }
}
