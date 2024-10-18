using Endatix.Core.Abstractions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// This factory is designed to create instances of the EfCore <see cref="ValueGenerator"/>. 
/// Currently, it supports Snowflake-based Id generation for long types. 
/// It can be extended to support more types, such as UUID7-based Guids.
/// </summary>
/// <param name="longIdGenerator">An instance of <see cref="IIdGenerator{long}"/> used for generating unique identifiers.</param>
public class EfCoreValueGeneratorFactory(IIdGenerator<long> longIdGenerator)
{
    /// <summary>
    /// Creates a ValueGenerator instance based on the provided property and entity.
    /// </summary>
    /// <typeparam name="T">The type of the value to be generated.</typeparam>
    /// <param name="property">The property for which a ValueGenerator is being created.</param>
    /// <param name="entity">The entity to which the property belongs. Optional.</param>
    /// <returns>A ValueGenerator instance.</returns>
    /// <exception cref="ArgumentException">Thrown if a ValueGenerator for a type other than long is requested.</exception>
    public ValueGenerator Create<T>(IProperty property, ITypeBase? entity = null) where T : IEquatable<T>
    {
        if (property.ClrType == typeof(long) && typeof(T).IsAssignableFrom(typeof(long)))
        {
            return new SnowflakeValueGenerator(longIdGenerator);
        }

        throw new ArgumentException("Invalid ValueGenerator output type passed to this factory function", nameof(property));
    }
}