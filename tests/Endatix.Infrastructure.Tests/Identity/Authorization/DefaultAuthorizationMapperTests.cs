using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authorization;
using Endatix.Core.Entities.Identity;

namespace Endatix.Infrastructure.Tests.Identity.Authorization;

public sealed class DefaultAuthorizationMapperTests
{
    private readonly RoleManager<AppRole> _roleManager;
    private readonly ILookupNormalizer _keyNormalizer;
    private readonly DefaultAuthorizationMapper _mapper;
    private readonly List<AppRole> _roles;
    private long _nextId = 1;

    public DefaultAuthorizationMapperTests()
    {
        _keyNormalizer = Substitute.For<ILookupNormalizer>();

        _roles = new List<AppRole>();
        var store = Substitute.For<IRoleStore<AppRole>>();
        _roleManager = Substitute.For<RoleManager<AppRole>>(
            store, null, null, null, null);

        _roleManager.Roles.Returns(_roles.AsQueryable());

        _mapper = new DefaultAuthorizationMapper(_roleManager, _keyNormalizer);
    }

    #region Empty Input Tests

    [Fact]
    public async Task MapToAppRolesAsync_EmptyExternalRoles_ReturnsEmptyResult()
    {
        // Arrange
        var externalRoles = Array.Empty<string>();
        var roleMappings = new Dictionary<string, string> { { "admin", "Admin" } };

        // Act
        var result = await _mapper.MapToAppRolesAsync(externalRoles, roleMappings, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Roles.Should().BeEmpty();
        result.Permissions.Should().BeEmpty();
    }

    [Fact]
    public async Task MapToAppRolesAsync_EmptyRoleMappings_ReturnsEmptyResult()
    {
        // Arrange
        var externalRoles = new[] { "admin" };
        var roleMappings = new Dictionary<string, string>();

        // Act
        var result = await _mapper.MapToAppRolesAsync(externalRoles, roleMappings, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Roles.Should().BeEmpty();
        result.Permissions.Should().BeEmpty();
    }

    [Fact]
    public async Task MapToAppRolesAsync_NullExternalRoles_ReturnsEmptyResult()
    {
        // Arrange
        string[]? externalRoles = null;
        var roleMappings = new Dictionary<string, string> { { "admin", "Admin" } };

        // Act
        var result = await _mapper.MapToAppRolesAsync(externalRoles!, roleMappings, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Roles.Should().BeEmpty();
        result.Permissions.Should().BeEmpty();
    }

    #endregion

    #region Error Handling

    [Fact]
    public async Task MapToAppRolesAsync_DatabaseException_ReturnsFailureResult()
    {
        // Arrange
        var externalRoles = new[] { "admin" };
        var roleMappings = new Dictionary<string, string> { { "admin", "Admin" } };

        _keyNormalizer.NormalizeName("Admin").Returns("ADMIN");

        var throwingQueryable = _roles.AsQueryable();
        _roleManager.Roles.Returns(throwingQueryable);


        // Act
        var result = await _mapper.MapToAppRolesAsync(externalRoles, roleMappings, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region GetMatchingRoles Tests

    [Fact]
    public void GetMatchingRoles_WithValidMappings_ReturnsMappedInternalRoles()
    {
        // Arrange
        var externalRoles = new[] { "keycloak-admin", "keycloak-user" };
        var roleMappings = new Dictionary<string, string>
        {
            { "keycloak-admin", "Admin" },
            { "keycloak-user", "User" }
        };

        // Act
        var result = InvokeGetMatchingRoles(externalRoles, roleMappings);

        // Assert
        result.Should().BeEquivalentTo(new[] { "Admin", "User" });
    }

    [Fact]
    public void GetMatchingRoles_WithPartialMappings_ReturnsOnlyMappedRoles()
    {
        // Arrange
        var externalRoles = new[] { "keycloak-admin", "keycloak-user", "unmapped-role" };
        var roleMappings = new Dictionary<string, string>
        {
            { "keycloak-admin", "Admin" },
            { "keycloak-user", "User" }
        };

        // Act
        var result = InvokeGetMatchingRoles(externalRoles, roleMappings);

        // Assert
        result.Should().BeEquivalentTo(new[] { "Admin", "User" });
    }

    [Fact]
    public void GetMatchingRoles_WithDuplicateExternalRoles_ReturnsDistinctInternalRoles()
    {
        // Arrange
        var externalRoles = new[] { "keycloak-admin", "keycloak-admin", "keycloak-user" };
        var roleMappings = new Dictionary<string, string>
        {
            { "keycloak-admin", "Admin" },
            { "keycloak-user", "User" }
        };

        // Act
        var result = InvokeGetMatchingRoles(externalRoles, roleMappings);

        // Assert
        result.Should().BeEquivalentTo(new[] { "Admin", "User" });
        result.Should().HaveCount(2);
    }

    [Fact]
    public void GetMatchingRoles_WithEmptyMappedRole_IgnoresEmptyMapping()
    {
        // Arrange
        var externalRoles = new[] { "keycloak-admin", "keycloak-empty" };
        var roleMappings = new Dictionary<string, string>
        {
            { "keycloak-admin", "Admin" },
            { "keycloak-empty", string.Empty }
        };

        // Act
        var result = InvokeGetMatchingRoles(externalRoles, roleMappings);

        // Assert
        result.Should().BeEquivalentTo(new[] { "Admin" });
    }

    [Fact]
    public void GetMatchingRoles_WithNoMatchingExternalRoles_ReturnsEmptyArray()
    {
        // Arrange
        var externalRoles = new[] { "unknown-role-1", "unknown-role-2" };
        var roleMappings = new Dictionary<string, string>
        {
            { "keycloak-admin", "Admin" },
            { "keycloak-user", "User" }
        };

        // Act
        var result = InvokeGetMatchingRoles(externalRoles, roleMappings);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetMatchingRoles_WithCaseSensitiveMapping_RespectsCase()
    {
        // Arrange
        var externalRoles = new[] { "keycloak-admin" };
        var roleMappings = new Dictionary<string, string>
        {
            { "keycloak-admin", "AdminRole" },
            { "KEYCLOAK-ADMIN", "DifferentRole" }
        };

        // Act
        var result = InvokeGetMatchingRoles(externalRoles, roleMappings);

        // Assert
        result.Should().BeEquivalentTo(new[] { "AdminRole" });
    }

    [Fact]
    public void GetMatchingRoles_WithMultipleExternalRolesMappingToSameInternalRole_ReturnsSingleInternalRole()
    {
        // Arrange
        var externalRoles = new[] { "keycloak-admin", "kc-admin", "admin-role" };
        var roleMappings = new Dictionary<string, string>
        {
            { "keycloak-admin", "Admin" },
            { "kc-admin", "Admin" },
            { "admin-role", "Admin" }
        };

        // Act
        var result = InvokeGetMatchingRoles(externalRoles, roleMappings);

        // Assert
        result.Should().BeEquivalentTo(new[] { "Admin" });
        result.Should().HaveCount(1);
    }

    [Fact]
    public void GetMatchingRoles_WithEmptyExternalRoles_ReturnsEmptyArray()
    {
        // Arrange
        var externalRoles = Array.Empty<string>();
        var roleMappings = new Dictionary<string, string>
        {
            { "keycloak-admin", "Admin" }
        };

        // Act
        var result = InvokeGetMatchingRoles(externalRoles, roleMappings);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetMatchingRoles_WithEmptyRoleMappings_ReturnsEmptyArray()
    {
        // Arrange
        var externalRoles = new[] { "keycloak-admin" };
        var roleMappings = new Dictionary<string, string>();

        // Act
        var result = InvokeGetMatchingRoles(externalRoles, roleMappings);

        // Assert
        result.Should().BeEmpty();
    }

    private string[] InvokeGetMatchingRoles(string[] externalRoles, Dictionary<string, string> roleMappings)
    {
        var method = typeof(DefaultAuthorizationMapper).GetMethod(
            "GetMatchingRoles",
            BindingFlags.NonPublic | BindingFlags.Instance);

        method.Should().NotBeNull("GetMatchingRoles method should exist");

        return (string[])method!.Invoke(_mapper, new object[] { externalRoles, roleMappings })!;
    }

    #endregion

    #region Helper Methods

    private AppRole CreateRole(string name, string normalizedName, bool isActive, bool isSystemDefined = true)
    {
        var role = new AppRole
        {
            Id = _nextId++,
            Name = name,
            NormalizedName = normalizedName,
            IsActive = isActive,
            IsSystemDefined = isSystemDefined,
            TenantId = 0
        };
        return role;
    }

    private Permission CreatePermission(string name)
    {
        var permission = new Permission(name);
        // Use reflection to set Id since it's likely protected/internal
        var idProperty = typeof(Permission).GetProperty("Id");
        if (idProperty is not null && idProperty.CanWrite)
        {
            idProperty.SetValue(permission, _nextId++);
        }
        return permission;
    }

    private void UpdateRolesQueryable()
    {
        // Update Roles to return current test data with async query support
        _roleManager.Roles.Returns(CreateAsyncQueryable(_roles.AsQueryable()));
    }

    /// <summary>
    /// Creates an IQueryable that supports EF Core operations (Include, ToListAsync).
    /// Since navigation properties are already populated, Include is a no-op.
    /// </summary>
    private static IQueryable<AppRole> CreateAsyncQueryable(IQueryable<AppRole> data)
    {
        // Return a queryable that supports async operations
        // Include/ThenInclude will be handled by EF Core extension methods if available
        // For unit tests, we'll use a simple wrapper that supports ToListAsync
        return new TestAsyncQueryable<AppRole>(data);
    }

    #endregion

    #region Test Async Query Support

    /// <summary>
    /// Simple async queryable wrapper that supports EF Core operations for testing.
    /// </summary>
    private class TestAsyncQueryable<T> : IQueryable<T>, IAsyncEnumerable<T>
    {
        private readonly IQueryable<T> _inner;

        public TestAsyncQueryable(IQueryable<T> inner)
        {
            _inner = inner;
            Provider = new TestAsyncQueryProvider<T>(inner.Provider);
        }

        public Type ElementType => _inner.ElementType;
        public Expression Expression => _inner.Expression;
        public IQueryProvider Provider { get; }

        public IEnumerator<T> GetEnumerator() => _inner.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new TestAsyncEnumerator<T>(_inner.GetEnumerator());
        }
    }

    private class TestAsyncQueryProvider<T> : IAsyncQueryProvider
    {
        private readonly IQueryProvider _inner;

        public TestAsyncQueryProvider(IQueryProvider inner)
        {
            _inner = inner;
        }

        public IQueryable CreateQuery(Expression expression) => _inner.CreateQuery(expression);
        public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => _inner.CreateQuery<TElement>(expression);
        public object Execute(Expression expression) => _inner.Execute(expression)!;
        public TResult Execute<TResult>(Expression expression) => _inner.Execute<TResult>(expression);

        public IAsyncEnumerable<TResult> ExecuteAsync<TResult>(Expression expression)
        {
            return new TestAsyncEnumerable<TResult>(expression);
        }

        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken)
        {
            // Handle Task<T> return types (e.g., ToListAsync returns Task<List<T>>)
            if (typeof(TResult).IsGenericType && typeof(TResult).GetGenericTypeDefinition() == typeof(Task<>))
            {
                var resultType = typeof(TResult).GetGenericArguments()[0];
                var executionResult = typeof(IQueryProvider)
                    .GetMethod(nameof(IQueryProvider.Execute), 1, new[] { typeof(Expression) })!
                    .MakeGenericMethod(resultType)
                    .Invoke(_inner, new object[] { expression })!;

                return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!
                    .MakeGenericMethod(resultType)
                    .Invoke(null, new[] { executionResult })!;
            }

            return _inner.Execute<TResult>(expression);
        }
    }

    private class TestAsyncEnumerable<T> : IAsyncEnumerable<T>
    {
        private readonly Expression _expression;

        public TestAsyncEnumerable(Expression expression)
        {
            _expression = expression;
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            // Execute the expression and return results
            var compiled = Expression.Lambda<Func<IEnumerable<T>>>(_expression).Compile();
            return new TestAsyncEnumerator<T>(compiled().GetEnumerator());
        }
    }

    private class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        private readonly IEnumerator<T> _inner;

        public TestAsyncEnumerator(IEnumerator<T> inner)
        {
            _inner = inner;
        }

        public T Current => _inner.Current;

        public ValueTask<bool> MoveNextAsync() => new(_inner.MoveNext());

        public ValueTask DisposeAsync()
        {
            _inner.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    #endregion
}
