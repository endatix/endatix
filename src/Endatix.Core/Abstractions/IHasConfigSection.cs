namespace Endatix.Core.Abstractions;

/// <summary>
/// Use this if you want to register AppSettings Config section alongside with your implementation
/// </summary>
/// <typeparam name="TSettings">The POCO class to be used for settings using the IOptions pattern <see cref="https://learn.microsoft.com/en-us/dotnet/core/extensions/options"/></typeparam>
public interface IHasConfigSection<TSettings> where TSettings : class
{
}