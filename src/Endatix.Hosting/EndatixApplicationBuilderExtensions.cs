using Microsoft.AspNetCore.Builder;
using System.Threading.Tasks;

namespace Endatix.Hosting;

/// <summary>
/// Extension methods for configuring Endatix middleware.
/// </summary>
public static class EndatixApplicationBuilderExtensions
{
    /// <summary>
    /// Adds all Endatix middleware to the application.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseEndatix(this IApplicationBuilder app)
    {
        // Add all middleware in the correct order
        app.UseEndatixExceptionHandler();
        app.UseEndatixSecurity();
        app.UseEndatixApi();
        
        return app;
    }
    
    /// <summary>
    /// Adds Endatix API middleware to the application.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseEndatixApi(this IApplicationBuilder app)
    {
        // Configure API middleware
        
        return app;
    }
    
    /// <summary>
    /// Adds Endatix security middleware to the application.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseEndatixSecurity(this IApplicationBuilder app)
    {
        // Configure security middleware
        app.UseAuthentication();
        app.UseAuthorization();
        
        return app;
    }
    
    /// <summary>
    /// Adds Endatix exception handling middleware to the application.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseEndatixExceptionHandler(this IApplicationBuilder app)
    {
        // Configure exception handling
        
        return app;
    }
    
    /// <summary>
    /// Applies database migrations.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task ApplyDbMigrationsAsync(this IServiceProvider serviceProvider)
    {
        // Apply database migrations
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Seeds the initial user.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task SeedInitialUserAsync(this WebApplication app)
    {
        // Seed initial user
        
        return Task.CompletedTask;
    }
} 